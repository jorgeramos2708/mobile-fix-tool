# BACKLOG M0–M2 — Plataforma de diagnóstico, recuperación y reparación

> Tareas asignables para los tres primeros hitos. Basado en el PLAN-MAESTRO v2.0 (§15, §17, §19–§25).
> Estimación en **días-persona (dp)**, no en días calendario. Versión 1.0.

---

## 0. Convenciones

**Roles:** `ARQ` arquitecto/lead · `HAL1..3` desarrollo de bajo nivel (USB/protocolos) · `UI1..2` interfaz ·
`CONT` contenido y tooling · `QA1..2` calidad con banco · `REL` release/DevOps · `LEG` legal/cumplimiento.

**Definition of Done por tipo de tarea:**

| Tipo | DoD |
|---|---|
| **Spike** | Funciona contra un dispositivo real del banco, con medición de tasa de éxito y fallos documentados. Código desechable, pero **hallazgos escritos en `docs/`** |
| **Contrato** | Interfaz + esquema congelado, con prueba unitaria que falla si cambia la forma |
| **Feature** | Código + pruebas unitarias + pruebas de integración contra el banco cuando aplique + documentación de operador |
| **Infra** | Reproducible desde cero por otra persona con un README, sin conocimiento tribal |

**Regla de admisión (§23):** nada entra si no sube cobertura, reduce bricks, o aumenta lo que el taller puede cobrar.

---

## 1. M0 — Spikes de viabilidad (4–6 semanas · ~106 dp)

**Objetivo:** saber con datos reales si los mecanismos son alcanzables con material legítimo, y congelar los
contratos que no se pueden retrofitear.

| ID | Tarea | Rol | dp | Depende de |
|---|---|---|---|---|
| M0-01 | Inventario y fichado del banco (150–300 equipos: SoC, mecanismo accesible, estado, casos) | QA1, QA2 | 8 | — |
| M0-02 | Hub USB con conmutación de energía por puerto + UPS + script de corte/restauración | HAL2, REL | 6 | — |
| M0-03 | **Spike Sahara/EDL**: handshake, SoC ID, lectura de GPT en Qualcomm | HAL1 | 10 | M0-01 |
| M0-04 | **Spike BROM**: handshake, chip ID, detección **DAA/SLA**, lectura de GPT en MediaTek | HAL2 | 10 | M0-01 |
| M0-05 | **Spike FDL** en Unisoc/Spreadtrum | HAL3 | 6 | M0-01 |
| M0-06 | **Spike EUB** en Exynos | HAL3 | 6 | M0-01 |
| M0-07 | **Spike DFU** + restore firmado vía idevicerestore en iPhone | HAL2 | 6 | M0-01 |
| M0-08 | **Spike fastboot/ADB** + lectura de `build.prop` y estado de bootloader | HAL3 | 4 | M0-01 |
| M0-09 | Instrumentación de medición de consumo para **firma de arranque** en el banco | HAL1 | 5 | M0-02 |
| M0-10 | **Matriz de cobertura v0** con tasa de éxito medida por mecanismo y modelo | ARQ, QA1 | 5 | M0-03…M0-08 |
| M0-11 | **Congelar contratos**: `OutcomeRecord`, `bench_id`, `IInstrument`, esquema de KB de síntomas | ARQ | 5 | M0-10 |
| M0-12 | Política de **procedencia** de loaders/auth files/firmware + registro con hash y origen | LEG, ARQ | 4 | — |
| M0-13 | ADRs 001–007 + estructura de repo + CI básico + convenciones de commit | ARQ, REL | 5 | — |
| M0-14 | **Prototipo Figma de la pantalla de sesión (13 etapas)** + validación 2 días con un técnico en banco | UI1, UI2 | 10 | — |
| M0-15 | Tokens v0 (`design/tokens`) con semántica de riesgo, enlace, etapas, cobertura | UI1 | 4 | M0-14 |
| M0-16 | Modelo de amenazas inicial de la propia herramienta | ARQ, HAL1 | 3 | — |
| M0-17 | **Informe de viabilidad go/no-go** + ajuste del alcance de M1 | ARQ | 3 | M0-10…M0-16 |

**Gate de salida M0:** 4 mecanismos hablan con dispositivo real · matriz de cobertura v0 medida · contratos
congelados · prototipo validado por un técnico real · política de procedencia firmada.
**Si un mecanismo no es alcanzable, se descubre aquí y no en el mes 12.**

---

## 2. M1 — Plataforma base (5–6 semanas · ~127 dp)

**Objetivo:** que instale, arranque, registre evidencia y se pueda actualizar. Sin esto, todo lo demás es demo.

| ID | Tarea | Rol | dp | Depende de |
|---|---|---|---|---|
| M1-01 | Solución hexagonal: proyectos, reglas de dependencia y **pruebas de arquitectura** (Domain sin I/O) | ARQ | 5 | M0-13 |
| M1-02 | **Journal append-only** con encadenado de hash + verificación de integridad + API de consulta | ARQ | 8 | M1-01 |
| M1-03 | `EvidenceStore`: almacenamiento, hashes, índice, exportación | ARQ | 6 | M1-02 |
| M1-04 | Persistencia SQLite: esquema v1, migrador propio, WAL, repositorios (ADR-004) | ARQ | 6 | M1-01 |
| M1-05 | **`DeviceHost` privilegiado**: servicio + named pipe, ACL del llamante, allow-list por operación | HAL1 | 10 | M1-01 |
| M1-06 | Administrador de drivers: detección de conflictos (Odin/Zadig/iTunes), `pnputil`, binding WinUSB | HAL2 | 8 | M1-05 |
| M1-07 | Enumeración USB (nivel L0) + observador estable de conexión/desconexión | HAL3 | 6 | M1-05 |
| M1-08 | **Verificación de calidad de enlace** (§21.10-1) + banco de pruebas de enlace degradado | HAL2 | 6 | M1-07 |
| M1-09 | **Plugin SDK v0**: manifiesto firmado, loader con validación, sandbox de permisos, ABI versionada | ARQ, HAL1 | 10 | M1-02 |
| M1-10 | Content: esquema de packs, validador, firma Ed25519, **raíz offline/HSM**, canales | CONT | 8 | M1-05 |
| M1-11 | Motor de licencias v0: licencia firmada, binding por huella, gracia offline, modo demo solo-lectura | CONT | 8 | M1-10 |
| M1-12 | **Instalador WiX v5 + firma Authenticode + auto-update** con canales stable/beta | REL | 8 | M1-01 |
| M1-13 | Shell UI: navegación, **barra de estado con salud de enlace**, tema desde tokens, i18n ES/PT | UI1, UI2 | 12 | M0-15, M1-12 |
| M1-14 | Puerto `IInstrument` + adaptador SCPI simulado + panel de instrumento | HAL1 | 6 | M0-11 |
| M1-15 | `OutcomeRecord`: esquema, escritura local, exportación (sin servidor) | ARQ | 4 | M1-04 |
| M1-16 | `bench_id` en todas las entidades + modelo de bloqueo por dispositivo (multi-puesto) | ARQ | 5 | M1-04 |
| M1-17 | Modelo de amenazas → hardening: integridad de packs, DPAPI, redacción de logs | ARQ, HAL1 | 5 | M0-16 |
| M1-18 | CI: build, tests, firma, artefactos versionados, HIL básico conectado | REL | 6 | M1-12 |

**Gate de salida M1:** `setup.msi` firmado instala y desinstala limpio en Win10 y Win11 · **ninguna escritura es
posible sin verificación de enlace y sin journal** · una licencia verificable offline activa el modo completo ·
un pack firmado inválido es rechazado.

---

## 3. M2 — Identificación universal (5–6 semanas · ~86 dp)

**Objetivo:** que la plataforma sepa qué tiene delante y qué puede hacer con ello, en modo solo-lectura.

| ID | Tarea | Rol | dp | Depende de |
|---|---|---|---|---|
| M2-01 | **Escalera L0–L6** completa con grado de confianza y detección de conflicto de identidad | HAL1, HAL2 | 12 | M1-07, M0-11 |
| M2-02 | **Signature DB**: esquema, ingesta local, matching, caché | ARQ, CONT | 8 | M2-01 |
| M2-03 | **Motor de cobertura**: matriz, resolución de veredictos, degradación a solo-lectura | ARQ | 8 | M2-01 |
| M2-04 | **Rule engine v1**: reglas declarativas cargadas desde packs firmados | ARQ | 8 | M1-09, M1-10 |
| M2-05 | **Device View**: árbol de dispositivos, panel de identidad, veredicto de cobertura visible antes de actuar | UI1, UI2 | 10 | M2-03, M1-13 |
| M2-06 | **Riesgo de brick v0**: heurística local + histórico del propio banco | ARQ, QA1 | 6 | M1-15, M2-03 |
| M2-07 | **Modo simulador v0**: reproducción de sesiones grabadas, dispositivos simulados | UI2 | 6 | M1-13 |
| M2-08 | Telemetría opt-in: agregados locales con **k-anonimato** (aún sin servidor) | CONT | 6 | M1-15 |
| M2-09 | **HIL en CI**: suite de regresión contra el banco en cada build | QA1, QA2, REL | 10 | M1-18 |
| M2-10 | Pruebas de caos de enlace: cortes en mitad de operación, enlace degradado, corrupción | HAL2, QA1 | 8 | M1-08, M2-09 |
| M2-11 | Manual de banco + matriz de cobertura publicada (interna y para venta) | QA2 | 4 | M2-09 |

**Gate de salida M2:** **≥85 %** del banco identificado a nivel modelo · veredicto de cobertura mostrado antes de
cualquier acción · riesgo de brick visible con explicación · matriz de cobertura publicada sin maquillaje.

---

## 4. Asignación y carga

| Rol | M0 | M1 | M2 | Nota |
|---|---|---|---|---|
| ARQ | ~21 dp | ~39 dp | ~30 dp | Cuello de botella real: es el único que puede tocar dominio, reglas y SDK |
| HAL1 | ~15 dp | ~26 dp | ~12 dp | Sahara/EDL + DeviceHost + instrumentación |
| HAL2 | ~16 dp | ~14 dp | ~14 dp | BROM + drivers + enlace + caos |
| HAL3 | ~10 dp | ~6 dp | — | FDL, EUB, fastboot: puede compartirse con M3 |
| UI1/UI2 | ~14 dp | ~12 dp | ~16 dp | Prototipo en M0 y Device View en M2 |
| CONT | — | ~16 dp | ~14 dp | Packs, licencias, telemetría |
| QA1/QA2 | ~13 dp | — | ~20 dp | Inventario en M0; regresión en M2 |
| REL | ~11 dp | ~14 dp | ~10 dp | Banco, CI, instalador |
| LEG | ~4 dp | — | — | Procedencia y uso aceptable |

**Riesgo de ejecución #1:** `ARQ` está sobrecargado en M1. Si el equipo tiene más de un senior de dominio, es ahí
donde debe entrar. Si no lo tiene, M1 se estira o se recorta (y lo que se recorta nunca es el journal ni la
verificación de enlace).

---

## 5. Lo que NO se hace en M0–M2 (por disciplina de alcance)

- Nada de escritura sobre dispositivos (llega en M5, con los gates completos).
- Nada de plugins OEM específicos (llegan en M7): en M0–M2 solo se construye el **mecanismo**.
- Nada de BI, inventario, mostrador ni WhatsApp (Fase 2).
- Nada de instrumentación real: solo el puerto y el simulador (la instrumentación es F1).
- Nada de Apple más allá del spike de DFU.
