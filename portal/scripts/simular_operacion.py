"""Simula operación real en una línea para ver funcionar alarmas, cobertura y KPIs.

Entra al portal con una cuenta ADMIN y, cada cierto tiempo, registra en algunas ubicaciones:
  - salidas marcadas como "Consumo de producción" (1 o 2 cajas, a ritmos distintos por ubicación);
  - cuando una ubicación se queda baja, una reposición completa: iniciar atención -> entrada -> finalizar atención.
El servidor pone la hora real a cada movimiento, por eso la simulación corre en vivo (no se pueden crear datos del pasado).

Las alarmas necesitan, por ubicación: horario de la línea configurado, 60 minutos laborables desde su apertura y
al menos 2 salidas de consumo en los últimos 60 minutos laborables. Con la configuración por defecto
(rondas cada 4 minutos) los primeros colores aparecen entre 10 y 20 minutos después de empezar.

Úsalo en una línea de pruebas (por ejemplo Línea 4) o en el portal local: los movimientos quedan en el historial.
Uso:
  python portal/scripts/simular_operacion.py --url https://tu-portal --usuario admin.alex --linea 4
  python portal/scripts/simular_operacion.py --url http://127.0.0.1:5087 --usuario admin.local --linea 2 --minutos 30 --ubicaciones 8
Opciones útiles: --intervalo 240 (segundos entre rondas), --crear-horario-prueba (si la línea no tiene horario),
--si (no pedir confirmación). La contraseña se pide en la terminal y no se guarda.

Prueba de carga (varios materialistas a la vez sobre la misma línea, con reintento ante choques como el portal):
  python portal/scripts/simular_operacion.py --url http://127.0.0.1:5099 --usuario persona0 --linea 1 \
      --sesiones 5 --ubicaciones 20 --intervalo 20 --minutos 15 --crear-horario-prueba --si \
      --password-archivo portal/.local/v2/user.password
Al final imprime operaciones, choques resueltos con reintento, fallos y tiempos de respuesta (mediana y p95).
"""
import argparse, getpass, http.cookiejar, json, random, statistics, sys, threading, time, uuid
import urllib.error, urllib.request
from datetime import datetime, timedelta, timezone

ESTADOS = {'AGOTADO': 'Agotado', 'RIESGO_PARO': 'Riesgo de paro', 'CRITICO': 'Crítico', 'ADVERTENCIA': 'Advertencia',
           'NORMAL': 'Normal', 'SIN_DATOS': 'Sin estimación'}


class Portal:
    def __init__(self, url):
        self.url = url.rstrip('/') + '/api'
        self.op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        self.token = ''

    def _req(self, path, body=None):
        headers = {}
        data = None
        if body is not None:
            data = json.dumps(body).encode()
            headers = {'Content-Type': 'application/json', 'X-XSRF-TOKEN': self.token, 'Idempotency-Key': str(uuid.uuid4())}
        req = urllib.request.Request(self.url + path, data=data, headers=headers, method='POST' if body is not None else 'GET')
        try:
            with self.op.open(req, timeout=60) as r:
                texto = r.read().decode() or '{}'
                return json.loads(texto)
        except urllib.error.HTTPError as e:
            try:
                msg = json.loads(e.read().decode()).get('error')
            except Exception:
                msg = None
            raise RuntimeError(f'{e.code}: {msg or "error del servidor"}') from None

    def csrf(self):
        self.token = self._req('/auth/csrf')['token']

    def login(self, usuario, password):
        self.csrf()
        self._req('/auth/login', {'username': usuario, 'password': password})
        self.csrf()  # el token se renueva con la sesión iniciada
        yo = self._req('/auth/me')
        if 'ajustar_catalogo' not in yo.get('permissions', []):
            raise RuntimeError('La cuenta debe ser ADMIN para registrar entradas, salidas y atenciones en cualquier ubicación.')
        return yo

    def estado(self, linea):
        return self._req(f'/v2/lineas/{linea}/estado')

    def operar(self, linea, tipo, body, version):
        return self._req(f'/v2/lineas/{linea}/operaciones/{tipo}', {**body, 'version': version})

    def operar_con_reintento(self, linea, tipo, body, stats=None, intentos=4):
        """Igual que el portal: si otra sesión registró en la línea al mismo tiempo (choque de versión), relee y reintenta."""
        for i in range(intentos):
            version = self.estado(linea)['version']
            inicio = time.perf_counter()
            try:
                r = self.operar(linea, tipo, body, version)
                if stats: stats.registrar(time.perf_counter() - inicio, i)
                return r
            except RuntimeError as ex:
                choque = str(ex).startswith('409: Los datos cambiaron') or str(ex).startswith('409: Línea ocupada')
                if not choque or i == intentos - 1:
                    if stats: stats.fallo(str(ex))
                    raise
                time.sleep(0.15 + random.random() * 0.35)


class Estadisticas:
    """Tiempos de respuesta, choques resueltos con reintento y fallos, compartidos entre sesiones."""
    def __init__(self):
        self.lock = threading.Lock(); self.tiempos = []; self.reintentos = 0; self.ops_con_choque = 0; self.fallos = []

    def registrar(self, segundos, reintentos):
        with self.lock:
            self.tiempos.append(segundos); self.reintentos += reintentos; self.ops_con_choque += 1 if reintentos else 0

    def fallo(self, motivo):
        with self.lock: self.fallos.append(motivo)

    def resumen(self):
        t = sorted(self.tiempos)
        if not t: return 'Sin operaciones registradas.'
        p95 = t[min(len(t) - 1, int(len(t) * 0.95))]
        return (f'{len(t)} operaciones registradas · {self.ops_con_choque} tuvieron choque y se resolvieron con reintento '
                f'({self.reintentos} reintentos) · {len(self.fallos)} fallos\n'
                f'Tiempo de respuesta: mediana {statistics.median(t)*1000:.0f} ms · p95 {p95*1000:.0f} ms · máximo {t[-1]*1000:.0f} ms')


def horario_prueba():
    hoy = datetime.now(timezone(timedelta(hours=-6))).date()
    return {'desde': (hoy - timedelta(days=1)).isoformat(), 'hasta': (hoy + timedelta(days=30)).isoformat(),
            'fuente': 'Horario de prueba para simulación (no es de planta)', 'dias': [0, 1, 2, 3, 4, 5, 6],
            'turnos': [{'inicio': '00:00', 'fin': '23:59', 'descansos': []}], 'excepciones': {},
            'normal': 60, 'advertencia': 30, 'critico': 15, 'simulado': True}


def elegir_ubicaciones(e, cuantas, semilla):
    mats = {m['id']: m for m in e['materiales']}
    racks = {r['id']: r for r in e['estanterias']}
    candidatas = [u for u in e['ubicaciones'] if u.get('activa', True) is not False and mats.get(u['material'], {}).get('activo')
                  and racks.get(u['estanteria'])]
    random.Random(semilla).shuffle(candidatas)
    # Perfiles de ritmo para que salgan colores distintos: cada cuántas rondas consume y cuántas cajas.
    perfiles = [('rápido', 1, 2), ('medio', 1, 1), ('lento', 2, 1)]
    return [(u['id'], perfiles[i % len(perfiles)]) for i, u in enumerate(candidatas[:cuantas])]


def nombre(e, uid):
    u = next(x for x in e['ubicaciones'] if x['id'] == uid)
    m = next(x for x in e['materiales'] if x['id'] == u['material'])
    r = next(x for x in e['estanterias'] if x['id'] == u['estanteria'])
    s = next((x['nombre'] for x in e['estaciones'] if x['id'] == r['estacion']), '?')
    return f"{m['numeroParte']} @ {s}/{r['nombre']}/{r['nivel']}"


def reponer(p, linea, e, u, m, stats=None):
    """Atención completa: iniciar -> entrada hasta llenar el espacio libre (máximo 6 cajas) -> finalizar."""
    cajas = max(0, min(6, u['libres']))
    if cajas == 0:
        return 'sin espacio libre para reponer'
    abierta = next((a for a in e['atenciones'] if a['ubicacion'] == u['id'] and not a['fin']), None)
    if not abierta:
        r = p.operar_con_reintento(linea, 'ATENCION_INICIAR', {'ubicacion': u['id']}, stats)
        abierta = {'id': r.get('id')}
    # Entrada que cierra la atención en la misma operación, como el portal.
    p.operar_con_reintento(linea, 'ENTRADA', {'ubicacion': u['id'], 'cajas': cajas, 'consumo': False, 'atencion': abierta['id'], 'finalizarAtencion': True}, stats)
    return f'reposición de {cajas} cajas ({cajas * m["empaque"]} pz) con atención'


def trabajar(p, linea, elegidas, rondas, intervalo, stats, etiqueta, detalle):
    """Una sesión: en cada ronda consume o repone sus ubicaciones. Con varias sesiones arranca con un desfase aleatorio."""
    if etiqueta: time.sleep(random.random() * min(intervalo, 10))
    for ronda in range(1, rondas + 1):
        if detalle: print(f"\n[{datetime.now().strftime('%H:%M:%S')}] Ronda {ronda}/{rondas}")
        for uid, (perfil, cada, cajas) in elegidas:
            e = None
            try:
                e = p.estado(linea)
                u = next(x for x in e['ubicaciones'] if x['id'] == uid)
                m = next(x for x in e['materiales'] if x['id'] == u['material'])
                cajas_con_saldo = -(-u['cantidad'] // m['empaque'])
                if cajas_con_saldo <= cajas:
                    accion = reponer(p, linea, e, u, m, stats)
                elif ronda % cada == 0:
                    p.operar_con_reintento(linea, 'SALIDA', {'ubicacion': uid, 'cajas': cajas, 'consumo': True, 'atencion': ''}, stats)
                    accion = f'consumo de {cajas} {"caja" if cajas == 1 else "cajas"}'
                else:
                    accion = 'sin movimiento'
            except RuntimeError as ex:
                accion = f'no se registró ({ex})'
            if detalle or accion.startswith('no se'):
                print(f"  {etiqueta+' ' if etiqueta else ''}{nombre(e, uid) if e else uid:<45} {accion}")
        if not detalle: print(f"[{datetime.now().strftime('%H:%M:%S')}] {etiqueta} ronda {ronda}/{rondas} lista")
        if ronda < rondas:
            time.sleep(intervalo * (0.8 + random.random() * 0.4) if etiqueta else intervalo)


def main():
    ap = argparse.ArgumentParser(description='Simula consumo y reposiciones para probar alarmas y KPIs.')
    ap.add_argument('--url', required=True, help='Dirección del portal, por ejemplo http://127.0.0.1:5087')
    ap.add_argument('--usuario', required=True, help='Cuenta ADMIN del portal')
    ap.add_argument('--linea', type=int, required=True, choices=[1, 2, 3, 4])
    ap.add_argument('--minutos', type=int, default=45, help='Duración total (por defecto 45)')
    ap.add_argument('--intervalo', type=int, default=240, help='Segundos entre rondas (por defecto 240 = 4 min)')
    ap.add_argument('--ubicaciones', type=int, default=6, help='Cuántas ubicaciones simular (por defecto 6)')
    ap.add_argument('--semilla', type=int, default=7, help='Para repetir la misma selección de ubicaciones')
    ap.add_argument('--crear-horario-prueba', action='store_true', help='Si la línea no tiene horario, crea uno de prueba 24/7')
    ap.add_argument('--sesiones', type=int, default=1, help='Sesiones en paralelo (materialistas a la vez) para prueba de carga')
    ap.add_argument('--password-archivo', help='Lee la contraseña de un archivo (por ejemplo portal/.local/v2/user.password)')
    ap.add_argument('--si', action='store_true', help='No pedir confirmación')
    a = ap.parse_args()

    p = Portal(a.url)
    if a.password_archivo:
        with open(a.password_archivo, encoding='utf-8') as f: a._password = f.read().strip()
    else:
        a._password = getpass.getpass(f'Contraseña de {a.usuario} (no se muestra al escribir): ')
    a.sesiones = max(1, min(a.sesiones, 20))
    yo = p.login(a.usuario, a._password)
    e = p.estado(a.linea)
    print(f"Sesión: {yo.get('displayName') or yo.get('name')} · Línea {a.linea} · {len(e['ubicaciones'])} ubicaciones")

    if not e.get('calendario'):
        if not a.crear_horario_prueba:
            sys.exit('La línea no tiene horario: configúralo en "Configurar línea" o usa --crear-horario-prueba.')
        p.operar_con_reintento(a.linea, 'CALENDARIO', horario_prueba())
        e = p.estado(a.linea)
        print('Horario de prueba creado (todos los días 00:00-23:59, marcado como simulado).')

    elegidas = elegir_ubicaciones(e, a.ubicaciones, a.semilla)
    if not elegidas:
        sys.exit('No hay ubicaciones activas con material activo en esta línea.')
    print('\nUbicaciones a simular:')
    for uid, (perfil, cada, cajas) in elegidas:
        print(f'  - {nombre(e, uid)} · ritmo {perfil}')
    rondas = max(1, a.minutos * 60 // a.intervalo)
    print(f'\n{rondas} rondas cada {a.intervalo // 60} min {a.intervalo % 60} s con {a.sesiones} sesión(es) en paralelo. Los movimientos quedan en el historial de la Línea {a.linea}.')
    if not a.si and input('¿Continuar? (s/n): ').strip().lower() not in ('s', 'si', 'sí'):
        sys.exit('Cancelado.')

    stats = Estadisticas()
    password = a._password
    # Cada sesión es un materialista distinto trabajando en paralelo sobre su parte de las ubicaciones.
    grupos = [elegidas[n::a.sesiones] for n in range(a.sesiones)]
    hilos = []
    for n, grupo in enumerate(grupos):
        if not grupo: continue
        sesion = p if n == 0 else Portal(a.url)
        if n > 0: sesion.login(a.usuario, password)
        h = threading.Thread(target=trabajar, args=(sesion, a.linea, grupo, rondas, a.intervalo, stats, f'S{n+1}' if a.sesiones > 1 else '', a.sesiones == 1), daemon=True)
        hilos.append(h); h.start()
    for h in hilos: h.join()
    e = p.estado(a.linea)
    print('\nAlarmas al terminar:')
    for uid, _ in elegidas:
        al = next((x for x in e['alarmas'] if x['ubicacion'] == uid), None)
        if al:
            r = al['resultado']
            minutos = '' if r['minutos'] is None else f" · {float(r['minutos']):.0f} min"
            print(f"  {nombre(e, uid):<45} {ESTADOS.get(r['estado'], r['estado'])}{minutos} ({r['causa']})")
    print('\n' + stats.resumen())
    if stats.fallos:
        print('Fallos (primeros 5): ' + ' | '.join(stats.fallos[:5]))
    print('\nListo. Revisa Resumen e Inventario (alarmas) y KPIs (actividad y diagnóstico) en el portal.')


if __name__ == '__main__':
    try:
        main()
    except RuntimeError as ex:
        sys.exit(f'Detenido: {ex}')
    except KeyboardInterrupt:
        sys.exit('\nDetenido por el usuario. Lo registrado hasta ahora se conserva.')
