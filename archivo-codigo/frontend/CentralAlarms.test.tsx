import React from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {describe,it,expect} from 'vitest';
import {CentralAlarms,RiskBadge,coverageLabel} from './CentralAlarms';
import type {User} from './api';
const user:User={id:1,name:'manager',roles:['gerencia'],permissions:['view','supervise'],simulado:true};
const data={counts:{RIESGO_PARO:1},rows:[{key:'a',part:'PARTE-UNO',station:'AL01',rack:'1',level:'B',post:'SZ01',state:'DISPONIBLE',severity:'RIESGO_PARO',color:'red',stock:100,coverageMinutes:10,priority:'ALTA / CRÍTICA',ratePiecesMinute:10,referenceMinutes:60,referenceConsumed:600,samples:2,explanation:'Cobertura inferior a 15 min',order:{id:44,reservedBoxes:0}}],history:[]};
function html(u=user){return renderToStaticMarkup(<CentralAlarms data={data} user={u} onChanged={()=>{}} onInventory={()=>{}} onSupply={()=>{}} onHistory={()=>{}}/>)}
describe('Alarmas sobre el histórico maestro',()=>{
 it('gerencia consulta severidad, cobertura, solicitud y procedencia sin acciones de escritura',()=>{const h=html();expect(h).toContain('RIESGO_PARO');expect(h).toContain('10 min');expect(h).toContain('Ver solicitud 44');expect(h).toContain('600');expect(h).not.toContain('Evaluar ahora y priorizar')});
 it('evaluación administrativa requiere motivo y advierte que usa el reloj actual',()=>{const h=html({...user,permissions:['view','admin']});expect(h).toContain('hora actual del servidor');expect(h).toMatch(/disabled=""[^>]*>Evaluar ahora y priorizar/)});
 it('el formato no redondea una cobertura urgente hacia 15 minutos',()=>{expect(coverageLabel(14.9999)).toBe('14.99 min');expect(coverageLabel(.001)).toBe('< 0.01 min');expect(coverageLabel(null)).toBe('N/D')});
 it('estado incluye icono y texto, sin depender solo del color',()=>{for(const state of ['NORMAL','ADVERTENCIA','CRITICO','RIESGO_PARO','AGOTADO','SIN_DATOS']){const h=renderToStaticMarkup(<RiskBadge state={state}/>);expect(h).toContain('<svg');expect(h).toContain('risk-badge')}});
});
