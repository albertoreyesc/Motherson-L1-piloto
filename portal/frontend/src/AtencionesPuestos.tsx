import {useEffect, useState, type FormEvent} from 'react';
import {api, date, nd, type User} from './api';
import {sitio, type Catalogo, type Inventario} from './MvpTypes';

type Atencion = {
  id_registro: number;
  puesto: string;
  numero_parte: string;
  id_ubicacion: number;
  fecha_hora_inicio: string;
  fecha_hora_fin: string | null;
  turno_inicio: string | null;
  duracion_ms: number | null;
};
type Pagina = {total: number; pagina: number; filas: Atencion[]};

export function AtencionesPuestos({user, ubicaciones, turnos, onHistorial}: {user: User; ubicaciones: Inventario[]; turnos: Catalogo['turnos']; onHistorial?:()=>void}) {
  const [datos, setDatos] = useState<Pagina>();
  const [seleccion, setSeleccion] = useState<Inventario>();
  const [buscar, setBuscar] = useState('');
  const [turno, setTurno] = useState('');
  const [ocupado, setOcupado] = useState(false);
  const [error, setError] = useState('');
  const captura = user.permissions.includes('atencion');

  async function consultar(pagina = 1, filtro = turno) {
    const query = new URLSearchParams({pagina: String(pagina)});
    if (filtro) query.set('turno', filtro);
    setDatos(await api<Pagina>('/atenciones?' + query));
    setTurno(filtro);
  }

  async function ejecutar(operacion: () => Promise<void>) {
    setOcupado(true);
    setError('');
    try { await operacion(); }
    catch (e) { setError((e as Error).message); }
    finally { setOcupado(false); }
  }

  useEffect(() => { void ejecutar(() => consultar()); }, []);
  const disponibles = ubicaciones.filter(u =>
    (u.numero_parte + ' ' + sitio(u)).toLowerCase().includes(buscar.trim().toLowerCase()));

  return <>
    {error && <p className="error" role="alert">{error}</p>}
    {captura && <section className="panel">
      <h2>Iniciar una atención</h2>
      <p>Selecciona la ubicación al empezar el trabajo. Finaliza la atención cuando termine.</p>
      <label className="field">Buscar parte o ubicación<input value={buscar} onChange={e => setBuscar(e.target.value)}/></label>
      <div className="button-row">{disponibles.map(u =>
        <button type="button" className="secondary" key={u.id_ubicacion} disabled={ocupado}
          aria-pressed={seleccion?.id_ubicacion === u.id_ubicacion} onClick={() => setSeleccion(u)}>
          {u.numero_parte} · {sitio(u)}
        </button>)}</div>
      <button disabled={ocupado || !seleccion} onClick={() => void ejecutar(async () => {
        if (!seleccion) return;
        await api('/atenciones/iniciar', {ubicacion: seleccion.id_ubicacion});
        setSeleccion(undefined);
        await consultar(1, '');
      })}>Iniciar atención{seleccion ? ' · ' + seleccion.numero_parte : ''}</button>
    </section>}
    <section className="panel">
      <h2>Intervalos de atención nuevos</h2><p>Un puesto inicia una atención en una ubicación y la finaliza al terminar. Su duración se calcula como fin menos inicio; una atención abierta no tiene duración final.</p>{!captura&&<p>Administración consulta estos intervalos; el registro lo realiza la cuenta del puesto. Las etapas y responsables ficticios del año simulado permanecen en el histórico y no se convierten automáticamente en atenciones nuevas.</p>}
      <p>Se guarda el turno de inicio en hora de Ciudad de México, aunque la atención termine en otro turno.</p>
      <label className="field">Filtrar por turno de inicio
        <select value={turno} disabled={ocupado} onChange={e => {
          const filtro = e.target.value;
          void ejecutar(() => consultar(1, filtro));
        }}>
          <option value="">Todos los turnos</option>
          {turnos.map(t => <option key={t.id_turno} value={t.id_turno}>{t.nombre_turno}</option>)}
          <option value="0">Fuera de turno</option>
        </select>
      </label>
      <p>{nd(datos?.total)} atenciones · página {datos?.pagina ?? 1}</p>
      <div className="table-wrap" role="region" aria-label="Atenciones" tabIndex={0}>
        <table><thead><tr>{['Puesto', 'Parte', 'Ubicación', 'Inicio', 'Fin', 'Turno al inicio', 'Duración', 'Estado']
          .map(t => <th key={t} scope="col">{t}</th>)}</tr></thead>
          <tbody>{datos?.filas.map(a => <tr key={a.id_registro}>
            <td>{a.puesto}</td><td>{a.numero_parte}</td><td>{a.id_ubicacion}</td>
            <td>{date(a.fecha_hora_inicio)}</td><td>{date(a.fecha_hora_fin)}</td><td>{a.turno_inicio ?? 'Fuera de turno'}</td>
            <td>{a.duracion_ms == null ? 'En curso' : nd(Math.floor(a.duracion_ms / 1000)) + ' s'}</td>
            <td>{a.fecha_hora_fin ? 'Finalizada' : captura
              ? <button className="secondary" disabled={ocupado} onClick={() => void ejecutar(async () => {
                await api('/atenciones/finalizar', {registro: a.id_registro});
                await consultar(datos?.pagina);
              })}>Finalizar atención #{a.id_registro}</button> : 'Abierta'}</td>
          </tr>)}</tbody>
        </table>
      </div>
      {datos?.filas.length === 0 && <div className="empty"><p>Aún no hay intervalos de atención registrados para esta consulta. Esto no significa que el histórico esté vacío.</p>{onHistorial&&<button className="secondary" onClick={onHistorial}>Ver movimientos y responsables del histórico</button>}</div>}
      <div className="button-row">
        <button className="secondary" disabled={ocupado || !datos || datos.pagina <= 1}
          onClick={() => datos && void ejecutar(() => consultar(datos.pagina - 1))}>Anterior</button>
        <button className="secondary" disabled={ocupado || !datos || datos.pagina * 50 >= datos.total}
          onClick={() => datos && void ejecutar(() => consultar(datos.pagina + 1))}>Siguiente</button>
      </div>
    </section>
  </>;
}

export function AccesosPuestos({puestos, onRefresh}: {puestos: Catalogo['puestos']; onRefresh: () => Promise<void>}) {
  const [puesto, setPuesto] = useState(0);
  const [usuario, setUsuario] = useState('');
  const [password, setPassword] = useState('');
  const [ocupado, setOcupado] = useState(false);
  const [mensaje, setMensaje] = useState('');
  const [error, setError] = useState('');
  const activo = puestos.find(p => p.id_puesto === puesto)?.acceso_habilitado;

  async function guardar(revocar: boolean) {
    setOcupado(true);
    setError('');
    setMensaje('');
    try {
      await api('/puestos/acceso', revocar ? {puesto, revocar} : {puesto, usuario, password, revocar});
      setPassword('');
      if (revocar) setUsuario('');
      setMensaje(revocar ? 'Acceso revocado.' : 'Acceso guardado.');
      await onRefresh();
    } catch (e) { setError((e as Error).message); }
    finally { setOcupado(false); }
  }

  function enviar(e: FormEvent) {
    e.preventDefault();
    void guardar(false);
  }

  return <section className="panel">
    <h2>Accesos por puesto</h2>
    <p>Configura una cuenta por puesto. Cambiar la contraseña o revocar el acceso cierra las sesiones anteriores.</p>
    {mensaje && <p role="status">{mensaje}</p>}
    {error && <p className="error" role="alert">{error}</p>}
    <form className="form-grid" onSubmit={enviar}>
      <label className="field">Puesto<select required value={puesto || ''} disabled={ocupado} onChange={e => {
        const id = Number(e.target.value);
        setPuesto(id);
        setUsuario(puestos.find(p => p.id_puesto === id)?.usuario ?? '');
        setPassword('');
      }}>
        <option value="">Selecciona un puesto</option>
        {puestos.map(p => <option key={p.id_puesto} value={p.id_puesto}>{p.nombre} · {p.acceso_habilitado ? 'habilitado' : 'sin acceso'}</option>)}
      </select></label>
      <label className="field">Usuario<input required value={usuario} maxLength={80} autoComplete="off" onChange={e => setUsuario(e.target.value)}/></label>
      <label className="field">Contraseña nueva<input required type="password" value={password} minLength={12} maxLength={512}
        autoComplete="new-password" onChange={e => setPassword(e.target.value)}/></label>
      <div className="button-row">
        <button disabled={ocupado || !puesto}>Guardar acceso</button>
        <button type="button" className="secondary" disabled={ocupado || !activo} onClick={() => void guardar(true)}>Revocar acceso</button>
      </div>
    </form>
  </section>;
}
