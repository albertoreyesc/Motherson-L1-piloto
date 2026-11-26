// Pantalla "Usuarios" (solo ADMIN): crear cuentas, asignar rol, líneas y puesto, cambiar contraseña y desactivar.
// El servidor y SQL vuelven a validar todo (mes2.UsuarioCrear / mes2.UsuarioActualizar).
import {useEffect,useState} from 'react';
import {Plus,ShieldCheck,UserRound,Eye,EyeOff} from 'lucide-react';
import {api} from './api';

export type RolUsuario='ADMIN'|'PUESTO'|'LECTOR';
export type AccesoUsuario={linea:number;puesto:string|null};
export type UsuarioPortal={id:number;usuario:string;nombre:string;rol:RolUsuario;activo:boolean;propio:boolean;accesos:AccesoUsuario[]};
export type LineaUsuarios={id:number;nombre:string;puestos:{id:string;nombre:string}[]};
type Datos={usuarios:UsuarioPortal[];lineas:LineaUsuarios[]};
type Form={id?:number;usuario:string;nombre:string;rol:RolUsuario;activo:boolean;password:string;confirmar:string;accesos:Record<number,string|null>};

export const ROLES:{id:RolUsuario;titulo:string;texto:string;icono:typeof ShieldCheck}[]=[
 {id:'ADMIN',titulo:'Administración',texto:'Ve y configura todo: catálogo, ubicaciones, usuarios, KPIs.',icono:ShieldCheck},
 {id:'PUESTO',titulo:'Puesto logístico',texto:'Solo Entradas y salidas e Inventario de los materiales de su puesto.',icono:UserRound},
 {id:'LECTOR',titulo:'Consulta',texto:'Resumen, Inventario, Historial, KPIs y Dashboard, sin editar.',icono:Eye},
];

/** Valida el formulario antes de enviarlo; devuelve el primer problema o '' si está completo. */
export function validarUsuario(f:Pick<Form,'id'|'usuario'|'nombre'|'rol'|'password'|'confirmar'|'accesos'>){
 if(!f.id&&!/^[A-Za-z0-9._-]{3,80}$/.test(f.usuario.trim()))return 'Usuario: de 3 a 80 caracteres, sin espacios ni acentos (letras, números, punto o guion).';
 if(!f.nombre.trim())return 'Escribe el nombre de la persona.';
 if(!f.id&&!f.password)return 'Escribe la contraseña inicial.';
 if(f.password&&f.password.length<12)return 'La contraseña debe tener al menos 12 caracteres.';
 if(f.password!==f.confirmar)return 'Las contraseñas no coinciden.';
 const lineas=Object.keys(f.accesos);if(!lineas.length)return 'Marca al menos una línea.';
 if(f.rol==='PUESTO'){const falta=lineas.find(l=>!f.accesos[Number(l)]);if(falta)return 'Elige el puesto de la Línea '+falta+'.'}
 return '';
}

const vacio=():Form=>({usuario:'',nombre:'',rol:'PUESTO',activo:true,password:'',confirmar:'',accesos:{}});

export function AdminUsuarios({dirty}:{dirty:(v:boolean)=>void}){
 const [datos,setDatos]=useState<Datos|null>(null),[error,setError]=useState(''),[aviso,setAviso]=useState(''),[form,setForm]=useState<Form|null>(null),[busy,setBusy]=useState(false),[buscar,setBuscar]=useState(''),[verInactivos,setVerInactivos]=useState(false),[verPass,setVerPass]=useState(false);
 async function cargar(){try{setDatos(await api<Datos>('/v2/usuarios'))}catch(ex){setError((ex as Error).message)}}
 useEffect(()=>{void cargar()},[]);
 const cambiar=(p:Partial<Form>)=>{setForm(f=>f&&{...f,...p});dirty(true)};
 const nombrePuesto=(linea:number,id:string|null)=>id?datos?.lineas.find(l=>l.id===linea)?.puestos.find(p=>p.id===id)?.nombre??id:null;
 function editar(u:UsuarioPortal){setError('');setAviso('');setForm({id:u.id,usuario:u.usuario,nombre:u.nombre,rol:u.rol,activo:u.activo,password:'',confirmar:'',accesos:Object.fromEntries(u.accesos.map(a=>[a.linea,a.puesto]))})}
 async function guardar(){if(!form)return;const problema=validarUsuario(form);if(problema){setError(problema);return}
  setBusy(true);setError('');setAviso('');
  try{
   const body:Record<string,unknown>={nombre:form.nombre.trim(),rol:form.rol,activo:form.activo,accesos:Object.entries(form.accesos).map(([linea,puesto])=>({linea:Number(linea),puesto:form.rol==='PUESTO'?puesto??'':''}))};
   if(form.id)body.id=form.id;else body.usuario=form.usuario.trim();
   if(form.password)body.password=form.password;
   await api('/v2/usuarios',body);
   setAviso(form.id?'Cambios guardados para '+form.usuario+'. Si cambiaste rol, contraseña o estado, su sesión abierta se cierra y debe volver a entrar.':'Usuario '+form.usuario.trim()+' creado. Ya puede iniciar sesión con su contraseña.');
   setForm(null);dirty(false);await cargar();
  }catch(ex){setError((ex as Error).message)}finally{setBusy(false)}}
 if(!datos)return <section className="panel"><h2>Usuarios</h2>{error?<p className="error" role="alert">{error}</p>:<p>Consultando usuarios…</p>}</section>;
 const lista=datos.usuarios.filter(u=>(verInactivos||u.activo)&&(u.usuario+' '+u.nombre).toLocaleLowerCase('es').includes(buscar.trim().toLocaleLowerCase('es')));
 const problema=form?validarUsuario(form):'';
 return <>
 {aviso&&<p className="notice" role="status">{aviso}</p>}
 {form?<section className="panel usuario-form" aria-busy={busy}>
  <h2>{form.id?'Editar '+form.usuario:'Nuevo usuario'}</h2>
  {error&&<p className="error" role="alert">{error}</p>}
  <div className="form-grid">
   <label className="field">Usuario (para iniciar sesión)<input value={form.usuario} disabled={!!form.id} autoComplete="off" maxLength={80} placeholder="Ej. juan.perez" onChange={ev=>cambiar({usuario:ev.target.value})}/></label>
   <label className="field">Nombre de la persona<input value={form.nombre} maxLength={120} placeholder="Ej. Juan Pérez" onChange={ev=>cambiar({nombre:ev.target.value})}/></label>
  </div>
  <h3>Rol</h3>
  <div className="config-opciones roles-opciones" role="radiogroup" aria-label="Rol del usuario">{ROLES.map(r=>{const Icono=r.icono;const bloqueado=!!form.id&&datos.usuarios.find(u=>u.id===form.id)?.propio&&r.id!=='ADMIN';return <button type="button" role="radio" aria-checked={form.rol===r.id} key={r.id} disabled={busy||bloqueado} title={bloqueado?'No puedes quitarte el rol de administración':undefined} className={'config-opcion'+(form.rol===r.id?' elegida':'')} onClick={()=>cambiar({rol:r.id})}><Icono size={22} aria-hidden="true"/><strong>{r.titulo}</strong><span>{r.texto}</span></button>})}</div>
  <h3>Líneas a las que entra</h3>
  <div className="usuario-lineas">{datos.lineas.map(l=>{const marcada=l.id in form.accesos;return <div key={l.id} className={'usuario-linea'+(marcada?' marcada':'')}>
   <label className="check"><input type="checkbox" checked={marcada} disabled={busy} onChange={ev=>{const a={...form.accesos};if(ev.target.checked)a[l.id]=null;else delete a[l.id];cambiar({accesos:a})}}/>{l.nombre}</label>
   {marcada&&form.rol==='PUESTO'&&<label className="field">Puesto en {l.nombre}<select value={form.accesos[l.id]??''} disabled={busy} onChange={ev=>cambiar({accesos:{...form.accesos,[l.id]:ev.target.value||null}})}><option value="">Selecciona el puesto</option>{l.puestos.map(p=><option key={p.id} value={p.id}>{p.nombre}</option>)}</select>{l.puestos.length===0&&<small className="block">Esta línea no tiene puestos; créalos en Configurar línea.</small>}</label>}
  </div>})}</div>
  <h3>{form.id?'Cambiar contraseña (opcional)':'Contraseña inicial'}</h3>
  <div className="form-grid">
   <label className="field">Contraseña<span className="campo-pass"><input type={verPass?'text':'password'} autoComplete="new-password" value={form.password} maxLength={512} placeholder="Mínimo 12 caracteres" onChange={ev=>cambiar({password:ev.target.value})}/><button type="button" className="ver-pass" aria-label={verPass?'Ocultar contraseña':'Mostrar contraseña'} aria-pressed={verPass} onClick={()=>setVerPass(!verPass)}>{verPass?<EyeOff size={18} aria-hidden="true"/>:<Eye size={18} aria-hidden="true"/>}</button></span>{form.password&&<small className={'block '+(form.password.length>=12?'coincide':'no-coincide')}>{form.password.length>=12?'✓ '+form.password.length+' caracteres':form.password.length+' caracteres · faltan '+(12-form.password.length)+' (mínimo 12)'}</small>}</label>
   <label className="field">Repite la contraseña<span className="campo-pass"><input type={verPass?'text':'password'} autoComplete="new-password" value={form.confirmar} maxLength={512} onChange={ev=>cambiar({confirmar:ev.target.value})}/><button type="button" className="ver-pass" aria-label={verPass?'Ocultar contraseña':'Mostrar contraseña'} aria-pressed={verPass} onClick={()=>setVerPass(!verPass)}>{verPass?<EyeOff size={18} aria-hidden="true"/>:<Eye size={18} aria-hidden="true"/>}</button></span>{form.confirmar&&<small className={'block '+(form.confirmar===form.password?'coincide':'no-coincide')}>{form.confirmar===form.password?'✓ Coinciden':'Todavía no coinciden'}</small>}</label>
  </div>
  <p className="help">Entrégale la contraseña a la persona en privado. El sistema la guarda cifrada y nadie puede volver a verla; si la olvida, aquí se le asigna una nueva.</p>
  {form.id&&!datos.usuarios.find(u=>u.id===form.id)?.propio&&<label className="check"><input type="checkbox" checked={form.activo} disabled={busy} onChange={ev=>cambiar({activo:ev.target.checked})}/>Cuenta activa (si la desactivas ya no podrá entrar; su historial se conserva)</label>}
  {problema&&<p className="help usuario-falta">Falta: {problema}</p>}
  <div className="button-row"><button disabled={busy||!!problema} onClick={()=>void guardar()}>{busy?'Guardando…':form.id?'Guardar cambios':'Crear usuario'}</button><button className="secondary" disabled={busy} onClick={()=>{setForm(null);setError('');dirty(false)}}>Cancelar</button></div>
 </section>:
 <section className="panel">
  <div className="panel-head"><div><h2>Usuarios del portal</h2><p className="help">{datos.usuarios.filter(u=>u.activo).length} activos · cada cuenta ve solo sus líneas y las secciones de su rol.</p></div><button onClick={()=>{setError('');setAviso('');setForm(vacio())}}><Plus size={18} aria-hidden="true"/>Nuevo usuario</button></div>
  {error&&<p className="error" role="alert">{error}</p>}
  <div className="form-grid"><label className="field">Buscar<input value={buscar} placeholder="Usuario o nombre" onChange={ev=>setBuscar(ev.target.value)}/></label></div>
  <label className="check"><input type="checkbox" checked={verInactivos} onChange={ev=>setVerInactivos(ev.target.checked)}/>Mostrar cuentas desactivadas</label>
  <div className="table-wrap"><table><thead><tr><th>Usuario</th><th>Rol</th><th>Líneas y puesto</th><th>Estado</th><th><span className="sr-only">Acciones</span></th></tr></thead><tbody>{lista.map(u=><tr key={u.id} className={u.activo?'':'fila-inactiva'}>
   <td><strong>{u.usuario}</strong>{u.propio&&<small className="block">Tu cuenta</small>}<small className="block">{u.nombre}</small></td>
   <td>{ROLES.find(r=>r.id===u.rol)?.titulo??u.rol}</td>
   <td>{u.accesos.length?u.accesos.map(a=><span className="block" key={a.linea}>{datos.lineas.find(l=>l.id===a.linea)?.nombre??'Línea '+a.linea}{a.puesto&&' · '+nombrePuesto(a.linea,a.puesto)}</span>):<span className="estado-pill inactivo">Sin líneas: no puede entrar</span>}</td>
   <td><span className={'estado-pill '+(u.activo?'activo':'inactivo')}>{u.activo?'Activa':'Desactivada'}</span></td>
   <td><button className="secondary small" onClick={()=>editar(u)}>Editar</button></td>
  </tr>)}</tbody></table></div>{lista.length===0&&<p className="empty">No hay usuarios con ese filtro.</p>}
 </section>}
 </>;
}
