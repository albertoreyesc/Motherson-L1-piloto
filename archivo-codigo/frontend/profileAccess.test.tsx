import React from 'react';
import {describe,it,expect} from 'vitest';
import {renderToStaticMarkup} from 'react-dom/server';
import {profileSections,initialSection,boundedCutTime} from './profileAccess';
import {AccountManagement,ShiftAccess,ProfileMatrix} from './AccessManagement';
import type {User} from './api';
const user=(role:string,permissions:string[]):User=>({id:1,name:'persona',roles:[role],permissions,simulado:true});
describe('matriz de perfiles y turnos',()=>{
 it('surtidor inicia en captura y no recibe pantallas administrativas',()=>{const u=user('materialista',['view','entry']);expect(initialSection(u)).toBe(6);expect(profileSections(u)).toEqual([6,1,7,3,4])});
 it('líder supervisa sin captura hasta que tenga cobertura y turno vigentes',()=>{const u=user('lider surtidores',['view','entry','supervise','stops']);expect(initialSection(u)).toBe(0);expect(profileSections(u)).not.toContain(6);expect(profileSections(u,true)).toContain(6);expect(profileSections(u,true)).not.toContain(5)});
 it('supervisor administra asignaciones sin captura ni ajustes',()=>{const u=user('supervisor logistica',['view','assign','supervise','stops']);expect(profileSections(u)).toContain(5);expect(profileSections(u)).not.toContain(6)});
 it('ingeniero y planeador inician en catálogo; gerente solo consulta',()=>{for(const r of ['ingeniero procesos','planeador']){const u=user(r,['view','catalog']);expect(initialSection(u)).toBe(5);expect(profileSections(u)).not.toContain(6)}expect(profileSections(user('gerencia',['view','supervise']))).toEqual([0,1,2,7,3,4])});
 it('el filtro del turno actual no solicita cierres futuros',()=>{expect(boundedCutTime('2026-10-27','15:00','2026-10-27T11:00:00-06:00')).toBe('11:00:00');expect(boundedCutTime('2026-10-27','23:30','2026-10-27T11:00:00-06:00')).toBe('11:00:00');expect(boundedCutTime('2026-10-05','15:00','2026-10-27T11:00:00-06:00')).toBe('15:00')});
 it('administrador mantiene las ocho secciones',()=>expect(profileSections(user('administrador',['view','entry','admin','catalog','assign']))).toHaveLength(8));
 it('gestión identifica turnos, vigencia, revocación y perfiles reales de acceso',()=>{const h=renderToStaticMarkup(<><ProfileMatrix/><ShiftAccess users={[]} rows={[]} onChanged={async()=>{}}/><AccountManagement users={[]} roles={['materialista','gerencia']} onChanged={async()=>{}}/></>);for(const s of ['PFEP T1','PFEP T2','Restablecer contraseña','Desactivar cuenta','Gerente · solo lectura','responsables ficticios'])expect(h).toContain(s);expect(h).toContain('type="password"')});
});
