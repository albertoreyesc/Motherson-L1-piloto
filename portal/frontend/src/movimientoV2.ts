import type {Material,Ubicacion} from './adminTypes';
export function preverMovimiento(m:Material,u:Ubicacion,tipo:'ENTRADA'|'SALIDA',cajas:number){
 if(!Number.isSafeInteger(cajas)||cajas<1||cajas>1000000)return {error:'Elige una cantidad entera de cajas entre 1 y 1,000,000.'};
 if(tipo==='ENTRADA'&&u.activa===false)return {error:'Ubicación desactivada: no admite entradas.'};
 if(tipo==='ENTRADA'&&!m.activo)return {error:'Material inhabilitado: solo se permite retirar el remanente.'};
 if(tipo==='ENTRADA'&&cajas>u.libres)return {error:'La estantería no tiene espacio para esa cantidad de cajas.'};
 if(tipo==='SALIDA'&&cajas>Math.ceil(u.cantidad/m.empaque))return {error:'No hay suficientes cajas en esta ubicación.'};
 const piezas=tipo==='ENTRADA'?cajas*m.empaque:Math.min(cajas*m.empaque,u.cantidad);
 const saldo=u.cantidad+(tipo==='ENTRADA'?piezas:-piezas);
 if(!Number.isSafeInteger(saldo)||saldo>2147483647||piezas<=0)return {error:'Cantidad fuera de rango.'};
 return {piezas,saldo};
}
