# PLAN MAESTRO — Plataforma profesional de diagnóstico, recuperación y reparación móvil (LATAM)

> Nombre en clave: **NAVAJA** (placeholder de trabajo — "navaja suiza de la reparación").
> Documento de arquitectura y ejecución. Versión 2.0.
> Estado: **plan aprobado en alcance, pendiente de M0**.
> La v2.0 incorpora cinco bloques nuevos: diagnóstico y triage (§19), ciclo de aprendizaje y telemetría
> de resultados (§20), operación de taller y robustez de ingeniería (§21), expansión del objetivo (§22)
> y priorización con regla de admisión (§23).

---

## 0. Decisiones cerradas

| # | Decisión | Implicación arquitectónica |
|---|---|---|
| 1 | Mercado **LATAM** | Prioridad de OEMs, variantes regionales (CSC/región), i18n ES-PT, cumplimiento multi-país (LFPDPPP, LGPD, Ley 25.326), verificación de propiedad del equipo obligatoria |
| 2 | Alcance **T1–T4** | Recuperación de datos es producto de primera clase, con sus propios gates de privacidad y límites de cifrado |
| 3 | **Apple entra** | Módulo Apple real (DFU, backup, diagnóstico, restauración firmada). Alcance honesto y acotado (ver §7) |
| 4 | Banco de pruebas disponible | El KPI de cobertura es medible desde M0 → el desarrollo se vuelve verificable, no especulativo |
| 5 | Personal disponible | El roadmap se planifica por rol, mapeable a tu equipo actual |
| 6 | **Licencia perpetua** | Obliga a resolver el financiamiento del contenido (ver §10). Es la decisión de negocio más delicada del plan |
| 7 | Capital disponible | El plan puede incluir banco instrumentado + CI hardware-in-the-loop desde el inicio |

---

## 1. Posicionamiento

**Producto:** plataforma de taller, no una caja de herramientas.

**Alcance funcional (T1–T4):**

| Nivel | Definición | Riesgo | Cobertura esperada |
|---|---|---|---|
| **T1 — Configuración** | Reparación sin tocar firmware: red, APN, sensores, calibración, batería, almacenamiento, MDM/cuenta | Bajo | Muy alta |
| **T2 — Boot** | Restaurar arranque: unbrick, bootloop, recovery, reflash de `boot`/`system`/`super`/`vbmeta` | Medio | Alta (por chipset) |
| **T3 — Partición** | Reparar particiones críticas: `persist`, `modem`, `efs`, `nvram`, `fsg`, `misc`, `frp` (restaurado desde su propio backup o firmware oficial) | Alto | Media-alta |
| **T4 — Datos** | Recuperar datos sin que arranque el SO: montaje/lectura de `userdata`, extracción, imagen para peritaje | Medio-alto | Media (limitada por cifrado) |
| **T5 — Hardware** | Fuera de alcance (sustitución de piezas, microsoldadura) | — | — |

**No-alcance permanente (en código, no en un PDF):** bypass de FRP/Activation Lock/iCloud Lock, reescritura o generación de IMEI, desbloqueo de equipos con propiedad no verificada, uso de loaders/auth files de origen filtrado, explotación de vulnerabilidades del bootrom para eludir bloqueos de activación (p. ej. clase `checkm8`), "desbloqueo de operador" mediante servicios no autorizados.

**Principio rector:** *la plataforma restaura la identidad propia del equipo; nunca crea una identidad nueva.* Esa distinción es la que la hace legal, vendible y defendible.

---

## 2. Columna vertebral: el pipeline de 13 etapas

Este es el hallazgo de diseño más importante del proyecto. El pipeline que describiste no es un flujo de UI: **es la máquina de estados del dominio.** Todo lo demás (UI, plugins, packs, reportes) se organiza alrededor de él.

```
Conectar → Identificar → Clasificar → Diagnosticar → Correlacionar
   → Comprobar restricciones → Respaldar → Analizar procedimiento
   → Safety Gate → Reparar → Verificar → Comparar antes/después → Reportar
```

### Propiedad de seguridad que se deriva de esto

> **Punto Único de Escritura (Single Write Point).**
> De las 13 etapas, **solo la etapa 10 (Reparar) puede escribir** en el dispositivo.
> Las etapas 1–7 son no destructivas por construcción. Las etapas 8–9 son análisis y autorización.
> Las etapas 11–13 son verificación y evidencia.
>
> La imposibilidad de escribir sin haber pasado por las 9 etapas anteriores no es una convención de código: es una restricción de tipos. El motor de reparación recibe un token de plan firmado que solo el Safety Gate puede emitir.

### Detalle por etapa

| # | Etapa | Módulo | Salida | Gate de bloqueo |
|---|---|---|---|---|
| 1 | **Conectar** | `Transport/HAL` | Enlace estable, modo detectado (L0) | Puerto/cable/enumeración, energía disponible |
| 2 | **Identificar** | `Identification` | Fingerprint L1–L6 + **grado de confianza %** | Ambigüedad → sondas adicionales o confirmación del técnico |
| 3 | **Clasificar** | `Coverage` | Veredicto: Pleno / Parcial / Solo lectura / Bloqueado / No soportado + nivel máximo alcanzable | No soportado → sesión degrada a solo-lectura |
| 4 | **Diagnosticar** | `Diagnostics` | Batería, almacenamiento, RAM, sensores, pantalla, touch, cámaras, audio, radios, temperatura, ciclos de carga, salud de particiones | Batería < umbral o lectura de almacenamiento inestable → bloquea cualquier escritura |
| 5 | **Correlacionar** | `Correlation` | Cruce síntoma reportado × diagnóstico × casos similares (base local + agregados anónimos) | Sin correlación → modo exploratorio (solo lectura) |
| 6 | **Comprobar restricciones** | `Constraints` | Bootloader, secure boot, AVB, ARB, MDM, cuenta vinculada, estado de bloqueo de activación (informado), tokens/loaders requeridos, versión firmada disponible | Restricción dura sin solución legítima → **imposible**, se comunica antes de tocar |
| 7 | **Respaldar** | `Backup` | GPT completa + particiones críticas + hash + **verificación de integridad de la copia** | Sin backup verificado → **ninguna escritura**, sin excepción |
| 8 | **Analizar procedimiento** | `RepairPlanner` | Plan auditable: qué, con qué fuente, hashes, orden, timeouts, rollback, criterio de éxito | Fuente sin verificar → plan rechazado |
| 9 | **Safety Gate** | `SafetyGates` | Consentimiento firmado, propiedad verificada, aprobación de supervisor si aplica, energía/UPS, riesgo declarado y aceptado | **Un solo flag en falso → bloqueo total** |
| 10 | **Reparar** | `RepairEngine` | Ejecución reanudable, journal por paso, aborto seguro, watchdog USB | Corte de enlace → detecta, no reintenta a ciegas, ofrece reanudar |
| 11 | **Verificar** | `Verification` | Hashes leídos vs esperados, arranque real, tests funcionales post | Fallo → **no se marca como éxito** (nunca "éxito optimista") |
| 12 | **Comparar antes/después** | `Diff` | Delta de particiones, versión, integridad, funcionalidad | Anomalía → alerta + evidencia |
| 13 | **Reportar** | `Reporting` | Informe cliente + informe técnico + evidencia + causa raíz + firma | **Siempre se emite, incluso (sobre todo) en fallo** |

### Reglas de la máquina de estados
- **Idempotente y reanudable:** cada etapa escribe su entrada/salida en el journal antes y después. Si el proceso muere, al reiniciar se lee el journal, se sondea el estado real del dispositivo y se ofrece continuar o abortar.
- **Bucle acotado:** `Verificar` en fallo puede devolver a `Analizar procedimiento` (nueva estrategia) con un máximo de N intentos. Sin ese límite, un técnico frustrado convierte el bucle en un brick.
- **No se salta etapas hacia adelante** (sí se puede abortar hacia atrás y reiniciar la sesión).
- **Toda sesión guarda las versiones exactas** de app, packs, adaptadores y loaders usados: sin eso no hay reproducibilidad ni defensa técnica.

### Agregado raíz: `RepairSession`
```
RepairSession (raíz)
├─ DeviceFingerprint (inmutable tras Identificar)
├─ CoverageVerdict
├─ DiagnosisReport
├─ ConstraintsReport
├─ BackupSet (con hashes y verificación)
├─ RepairPlan (firmado por SafetyGate)
├─ ExecutionJournal (append-only, encadenado por hash)
├─ VerificationReport
├─ DiffReport
└─ Report (cliente + técnico)
```

---

## 3. Arquitectura de capas (diagrama corregido)

```
┌───────────────────────────────────────────────────────────────┐
│ PRESENTACIÓN — Dashboard · Device View · Diagnostics ·        │
│                Firmware · Recovery · Security · Evidence ·     │
│                Service Orders · Reports                        │
└──────────────────────────┬────────────────────────────────────┘
                           │
┌──────────────────────────▼────────────────────────────────────┐
│ APLICACIÓN — RepairSession Orchestrator · Workflow Engine ·    │
│              Repair Engine · Safety Gates · Planner ·          │
│              Verification · Reporting · Orders · Authorization │
└──────────────────────────┬────────────────────────────────────┘
                           │
┌──────────────────────────▼────────────────────────────────────┐
│ DOMINIO / CORE — Device Model · Identity · Capabilities ·      │
│   Diagnostics · Security · Firmware · Recovery · Evidence ·    │
│   Fingerprint · Compatibility · Rules · Coverage               │
│   (cero dependencias de I/O — testeable sin hardware)          │
└──────────────────────────┬────────────────────────────────────┘
                           │
        ┌──────────────────┼──────────────────┬───────────────┐
        ▼                  ▼                  ▼               ▼
┌──────────────┐  ┌──────────────┐  ┌───────────────┐  ┌─────────────┐
│ IDENTIFICACIÓN│ │ MOTORES POR  │  │ PLUGINS       │  │ CONTENIDO   │
│ Escalera L0-L6│ │ MECANISMO    │  │ OEM           │  │ Knowledge   │
│ + Signature DB│ │ MTK/QC/UNI/  │  │ Samsung/Xiaomi│  │ Packs       │
│               │ │ EXYNOS/APPLE │  │ Motorola/Tecno│  │ firmados    │
└──────┬───────┘  └──────┬───────┘  └──────┬────────┘  └─────────────┘
       │                 │                 │
       └─────────────────┼─────────────────┘
                         │
┌────────────────────────▼──────────────────────────────────────┐
│ INFRAESTRUCTURA / HAL — Transporte (USB, Serial, TCP-USB) ·   │
│   Protocolos (ADB, Fastboot, EDL/Sahara, BROM, FDL, EUB, DFU,  │
│   muxd) · Windows (WinUSB/libusb, drivers, pnputil, procesos)  │
│   · Filesystem · Servicios externos                            │
└────────────────────────┬──────────────────────────────────────┘
                         │
┌────────────────────────▼──────────────────────────────────────┐
│ DISPOSITIVO FÍSICO — Samsung · Xiaomi · Motorola · Tecno ·     │
│   Infinix · itel · ZTE · OPPO · realme · Huawei · Apple · etc. │
└───────────────────────────────────────────────────────────────┘
```

**Diferencias clave respecto al diagrama original:**
1. El segundo bloque "APPLICATION" era en realidad **DOMINIO/CORE**.
2. Plataforma / Chipset / Fabricante dejan de ser un fork de tres ramas y pasan a ser **dimensiones ortogonales** resueltas por una matriz de cobertura. Un equipo es un vector: `(Android, MediaTek Helio G85, Xiaomi, región LATAM, Android 12, T3)`.
3. El HAL se separa en **transporte**, **protocolos** y **entorno Windows** (drivers), que es donde vive el 80% del riesgo real.
4. Se añaden dos capas que el diagrama no contemplaba: **Identificación** (escalera de sondas) y **Contenido** (Knowledge Packs), que son el verdadero foso competitivo.

---

## 4. Motor de identificación: escalera de fingerprint

Un equipo que no enciende no dice qué es. La identificación es una **cascada de sondas con intrusividad creciente**, y todas deben funcionar con el equipo muerto.

| Nivel | Sonda | Se obtiene | Intrusión |
|---|---|---|---|
| **L0** | Descriptor USB: VID/PID, clase, strings iManufacturer/iProduct | Familia / a veces modelo | Nula |
| **L1** | Interfaces y endpoints (QDLoader 9008, MTK Preloader/BROM, Spreadtrum, DFU, ADB, muxd) | **Modo activo** → qué protocolo hablar | Nula |
| **L2** | Handshake: Sahara (SoC ID, MSM ID, PK hash, serial, OEM ID) · BROM (chip ID, HW code/subcode, SW ver) · FDL · EUB | **SoC exacto**, secure boot flags | Solo handshake |
| **L3** | Lectura de GPT + MBR por BROM/EDL (solo lectura) | Layout de particiones y tamaños | Lectura |
| **L4** | Lectura de `build.prop`/`ro.build.*`, `bootloader`, `vendor`, `modem` | Modelo, región, build, CSC | Lectura |
| **L5** | `otp`/`persist`/`nvdata`, board ID, revisión de HW | Identidad de placa (compatibilidad de repuestos) | Lectura |
| **L6** | Baseband/IMEI (**solo lectura y hasheado**) | Confirmación de identidad | Lectura |

**Requisitos:**
- La escalera completa corre en **modo solo lectura**. Existe un modo "diagnóstico seguro" que se detiene en L6 y nunca abre camino a escritura.
- Cada nivel emite un **grado de confianza**. Si L5 y L6 discrepan → conflicto de identidad → se marca como "identidad dudosa" (señal típica de equipo con reparación previa o placa cambiada, muy común en LATAM).
- Resultado alimenta la **Device Signature Database**.

### Device Signature Database (activo estratégico)
`(VID/PID + strings USB + huella de endpoints + respuesta de handshake) → chipset + placa + modelo + variante`.

- Se alimenta con **telemetría opt-in, anonimizada y hasheada** desde M2.
- Efecto de red: más talleres → identificación instantánea de equipos desconocidos.
- Es el activo que la competencia acumuló durante años. Sin él, cada modelo nuevo es investigación manual.

---

## 5. Motor de cobertura (la matriz)

**Dimensiones:** `plataforma × SoC × OEM × versión de OS × variante regional × nivel (T1–T4)`

**Veredictos:**

| Veredicto | Significado para el técnico |
|---|---|
| **Pleno** | Todas las operaciones del nivel están soportadas y verificadas |
| **Parcial** | Soportadas algunas; la UI indica cuáles y por qué |
| **Solo lectura** | Identificación, diagnóstico, backup y recuperación de datos; sin escritura |
| **Bloqueado por autorización** | Técnicamente posible, requiere token/loader legítimo/supervisor |
| **No soportado** | Sin ruta; se explica la razón técnica |

**Por qué esto es un diferenciador y no una debilidad:** la competencia promete "todo" y brickea equipos. Un taller con seguro necesita saber **antes** qué va a poder hacer. La matriz visible en la UI, antes de tocar nada, es lo que convierte a NAVAJA en herramienta profesional.

### Prioridad de OEM para LATAM (orden de inversión de esfuerzo)

| Prioridad | OEM / familia | Justificación |
|---|---|---|
| 1 | **Samsung** (Exynos + Qualcomm + MTK) | Mayor volumen instalado en la región; protocolo propio |
| 2 | **Xiaomi / Redmi / POCO** (MTK + Qualcomm) | Volumen muy alto; atención a ARB y vinculación de cuenta |
| 3 | **Motorola** (Qualcomm) | Fuerte en MX/BR/AR |
| 4 | **Tecno / Infinix / itel** (MTK + Unisoc) | Dominan gama de entrada; poco atendidos por la competencia → nicho |
| 5 | **ZTE / Honor / OPPO / realme / vivo** (MTK + Qualcomm) | Volumen creciente |
| 6 | **Huawei** (HiSilicon) | Solo lectura / datos |
| 7 | **Apple** | Ver §7 |

---

## 6. Motores de bajo nivel (por mecanismo, no por OEM)

| Mecanismo | Plataformas | Restricción real | Habilitades |
|---|---|---|---|
| **MediaTek BROM / Preloader** | Android MTK (mayoría gama media/baja) | Chips modernos con autenticación **DAA/SLA** → requieren *auth files* legítimos | L2–L5, backup, T2/T3 |
| **Qualcomm EDL (9008) + Sahara + Firehose** | Android QC | **Programador firehose firmado**; SoCs nuevos con autenticación | L2–L5, backup, T2/T3 |
| **Unisoc / Spreadtrum BROM + FDL** | Gama de entrada | Secure boot en algunos; requiere .pac / FDL legítimos | L2–L5, T2 |
| **Exynos EUB / Odin** | Samsung Exynos | Protocolo propietario; modelos nuevos con token remoto | L2–L5, T2/T3 (según modelo) |
| **Fastboot / ADB / Recovery** | Android estándar | Requiere bootloader desbloqueado para escritura; ADB requiere sistema arrancado | T1, T2, T4, T3 (parcial) |
| **Apple DFU + usbmuxd** | iPhone/iPad | **Verificación de firma (SHSH)**: solo restauras la versión que Apple firma. Sin downgrade | Restore firmado, backup, diagnóstico (ver §7) |

**Regla inquebrantable:** solo loaders, DA, auth files y firmware **obtenidos por vía legítima**. La distribución de material filtrado es el riesgo legal número uno del proyecto y hay que gestionarlo como riesgo de ingeniería (política de procedencia documentada por archivo, con hash y origen).

---

## 7. Apple — alcance real

Entra en el producto, pero con un alcance definido por la criptografía, no por nuestra ambición:

### Lo que SÍ se hace
| Capacidad | Mecanismo | Nivel |
|---|---|---|
| Detección y modo (normal / recovery / DFU / deshabilitado / sin SO) | USB + usbmuxd | L0–L1 |
| Modelo, versión de iOS, build, estado de activación (**solo lectura, informado**), IMEI/serial, capacidad, batería y ciclos | libimobiledevice / idevicediagnostics | L4–L6 |
| **Salida de modo recovery/DFU** | idevicerestore (a versión firmada) | T2 |
| **Restauración firmada** (recupera arranque, y con "actualizar" puede preservar datos) | idevicerestore + TSS | T2 |
| **Backup completo y restauración** de backup del dueño | dispositivo + credencial del dueño | T4 |
| **Extracción de datos desde backups** existentes en el PC/taller | análisis de backup | T4 |
| Diagnóstico funcional (pantalla, touch, audio, sensores, cámaras, radios, batería) | rutinas de iOS + lectura | T1 |
| Informe y evidencia para el cliente | núcleo de la plataforma | Todas |

### Lo que NO se hace (permanente)
- Bypass de Activation Lock / iCloud Lock.
- Downgrade a versiones no firmadas.
- Explotación del bootrom (clase `checkm8`) para instalar código no autorizado o eludir bloqueos, en ninguna forma.
- Extracción del passcode del usuario.
- Extracción del sistema de archivos en dispositivos bloqueados.

**Consecuencia para el negocio:** el módulo Apple se vende honestamente como *"recuperación de arranque, backup y recuperación de datos con credenciales del propietario"*. En LATAM esto cubre un volumen enorme de casos reales (equipo que no arranca, pantalla de restauración, actualización fallida, migración de datos) sin tocar el terreno prohibido.

---

## 8. T4 — Recuperación de datos y sus límites reales

Necesitas ser brutalmente honesto aquí, porque las expectativas del taller son altas:

| Escenario Android | ¿Recuperable? | Cómo |
|---|---|---|
| Arranca y el dueño da credencial | **Sí, alta tasa** | Extracción vía ADB/MTP, apps del sistema, contactos, media |
| No arranca, `userdata` intacta, **sin cifrado** | **Sí** | Lectura directa de `userdata` por EDL/BROM |
| No arranca, `userdata` intacta, **con FBE/FDE** | **NO sin la credencial del dueño** | Cifrado por hardware desde Android 6+ es el muro real |
| Backup previo existente | **Sí** | Restauración y extracción |
| Daño físico del almacenamiento (eMMC/UFS) | **Fuera de alcance** | Requiere laboratorio de chip-off, no es software |

**Lo mismo aplica a iOS:** con credenciales y backup, alta tasa; sin credenciales, no.

Este cuadro debe aparecer **en el informe que firma el cliente**, antes de aceptar el trabajo. Es la diferencia entre un taller profesional y uno que promete magia.

---

## 9. Content Platform: Knowledge Packs

El conocimiento vive en **datos firmados**, nunca en código. Añadir 5.000 modelos no debe requerir un release de la aplicación.

```
packs/
├─ capabilities/mtk-brom.pack          # sondeo, handshakes, auth, timeouts
├─ capabilities/qc-sahara-firehose.pack
├─ capabilities/unisoc-fdl.pack
├─ capabilities/exynos-eub.pack
├─ capabilities/apple-dfu.pack
├─ partitions/gpt-heuristics.pack      # patrones de nombres por OEM/familia
├─ constraints/secureboot-avb-arb.pack # reglas de restricción dura
├─ firmwares/<oem>-<region>.pack       # catálogo con hash + firma + procedencia
├─ rules/*.pack                        # reglas de seguridad y compatibilidad
└─ oem/<oem>.pack                      # protocolos propietarios
```

**Requisitos del pipeline de contenido:**
- Firma **Ed25519** por pack; manifiesto con versión de ABI, compatibilidad de app, autor y hash.
- Canales `stable` / `beta` / `lab`.
- **Validación en CI**: un pack que no pase el esquema y la suite de regresión no se publica.
- La app **rechaza** packs con firma inválida o ABI incompatible; nunca "los ejecuta igualmente".
- Distribución online + importación manual offline (talleres con internet inestable — realidad LATAM).
- Procedencia documentada de cada firmware/loader: origen, licencia, hash. **Es un requisito legal, no administrativo.**

---

## 10. Licenciamiento perpetuo: el problema a resolver

Aquí hay una tensión que hay que cerrar antes de vender la primera licencia:

> Una licencia perpetua pagada una vez **no financia** el coste perpetuo del catálogo de firmware, los packs, los loaders autorizados y el testing hardware. Los competidores resuelven esto con suscripción; tú necesitas una estructura perpetua que siga viva.

**Propuesta (a validar contigo):**

| Componente | Modelo |
|---|---|
| **Aplicación** | Licencia **perpetua** de la versión mayor adquirida |
| **Ventana de actualizaciones** | Incluida por 24 meses desde la compra; después, renovación opcional para recibir nuevas versiones mayores |
| **Knowledge Packs** | Actualización incluida durante la ventana; después, "**Content Pass**" anual opcional |
| **Módulos** | Apple / T4 / Forense como módulos licenciables aparte |
| **Multi-técnico** | Licencia por puesto; licencia de cadena con consola de administración |
| **Tier Enterprise** | API, despliegue por Intune, auditoría centralizada, reportes consolidados |

**Mecánica técnica de la licencia (offline-first, crítico para LATAM):**
- Archivo de licencia firmado con **Ed25519**, verificable sin internet.
- **Binding** por huella del equipo (hash de componentes de hardware) con tolerancia a cambios menores.
- Modo demo limitado (solo lectura) sin licencia — permite que el taller pruebe antes de comprar.
- Opcional: **dongle USB** como alternativa de activación en talleres con múltiples PCs.
- Periodo de gracia offline (p. ej. 30 días) para evitar que un corte de internet detenga a un taller.
- Revocación solo para licencias comprometidas (lista firmada, no un "kill switch" que apaga equipos legítimos).

---

## 11. Seguridad, legal y cumplimiento

### Verificación de propiedad (obligatoria en LATAM)
Todo sesión que implique escritura o acceso a datos requiere:
1. Identificación oficial del cliente capturada.
2. **IMEI/serial registrado en la orden** junto a los datos del cliente.
3. Declaración firmada de propiedad y de autorización para manipular el dispositivo.
4. Registro de que el checklist se completó (queda en el journal, protege al taller).

Esto es simultáneamente una obligación ética, una protección legal para el taller y una función de producto que la competencia no tiene.

### Cumplimiento por país
| País | Norma de datos personales | Norma de consumo/telecom |
|---|---|---|
| México | LFPDPPP | PROFECO / IFT |
| Brasil | **LGPD** | Anatel / CDC |
| Argentina | Ley 25.326 | ENACOM / Defensa del Consumidor |
| Colombia / Chile / Perú | Ley 1581 / Ley 19.628 / Ley 29733 | Reguladores locales |

Implicaciones: minimización de datos, retención configurable, borrado a petición, aviso de privacidad, y **hash de IMEI/serial** en toda telemetría. Datos de dispositivos de clientes nunca salen del taller sin opt-in explícito.

### Seguridad de la propia herramienta
- Firma **Authenticode EV** de instalador, binarios, packs, plugins y actualizaciones. Sin esto, SmartScreen y los antivirus marcan las herramientas de flasheo como PUA y el taller no puede ni instalar. **No es opcional.**
- **Helper privilegiado** (`DeviceHost`): la UI nunca corre como administrador; el helper expone una API *allow-list* por operación y valida el proceso llamante.
- Verificación de integridad de packs y plugins en cada arranque; si no cuadra → modo degradado de solo-lectura.
- Secretos en DPAPI / Credential Manager; nunca en JSON.
- Redacción de logs (IMEI, serial, teléfono); sin telemetría por defecto.

### Línea adicional de negocio (opcional, alto margen)
**Modo forense/pericial:** exportación de evidencia con cadena de custodia formal (journal encadenado por hash + firma + sellado temporal) para despachos jurídicos, aseguradoras y peritajes. En LATAM hay demanda real y muy poca oferta local profesional. El mismo motor de evidencia que ya necesitas para el taller.

---

## 12. Instalador y despliegue

- **Stack:** .NET 9 + WinUI 3 (o WPF si aparece fricción de empaquetado), MVVM, WPF/WinUI para UI y dominio; helper privilegiado separado. Alternativa Tauri 2 + Rust si tu equipo es mayoritariamente web (decisión en §18).
- **Instalador:** MSI con **WiX v5** (o Inno Setup); instalación per-machine; runtime self-contained.
- **Auto-update firmado** (Velopack o MSIX App Installer) con canales stable/beta.
- **Datos:** `%PROGRAMDATA%\NAVAJA\` (catálogo, packs, evidencia, licencias) · `%LOCALAPPDATA%` (caché por usuario).
- **Desinstalación limpia** que no elimine drivers del sistema sin confirmación explícita.
- **Convivir** con Odin, SP Flash Tool, iTunes y Zadig sin romper drivers ajenos: es una función, no un detalle. Diagnóstico de conflictos de driver dentro de la UI.
- Licencias de terceros a revisar antes de empaquetar: platform-tools (Apache-2.0), libusb (LGPL-2.1, enlace dinámico), libimobiledevice (LGPL), .NET (MIT). Evitar cualquier componente GPL/Qt.

---

## 13. Banco de pruebas e infraestructura de regresión

Como ya cuentas con banco, el objetivo es convertirlo en **CI hardware-in-the-loop**, porque eso es lo que permite prometer cobertura.

**Requerido:**
- **150–300 equipos** cubriendo: 5 mecanismos × 8–10 OEMs × gama baja/media/alta × variantes de región (incluye equipos "muertos" e in-brick, que son los casos que importan).
- **Hubs USB con conmutación de energía por puerto** — imprescindible para probar caídas de enlace y reanudación, que es el escenario de fallo más común.
- UPS en el rack de pruebas.
- **Inventario de dispositivos** en base de datos con estado, chipset, ubicación en el hub y casos de prueba asignados.
- **Runner de regresión**: antes de cada release y de cada pack se ejecuta la suite completa contra el banco. Resultado = la matriz de cobertura, publicada.
- Cadencia de reposición: el mercado cambia cada ~6 meses; el banco es gasto de capital **continuo**, no único.
- Rigs específicos: EDL/9008, BROM con punto de test (para autenticación y para equipos sin botón), EUB, DFU.

---

## 14. Equipo (mapa de roles sobre tu personal actual)

| Rol | FTE | Foco | Nota |
|---|---|---|---|
| Arquitecto / lead | 1 | Dominio, SDK de plugins, decisiones de arquitectura | Perfil más crítico y más difícil de sustituir |
| Devs HAL / protocolos | 2–3 | Sahara, BROM, FDL, EUB, DFU, USB, drivers | **Grupo escaso**. Sin él, el proyecto no existe |
| Devs UI / aplicación | 2 | WinUI/WPF, órbita del técnico, flujos de 13 etapas | — |
| Dev contenido/tooling | 1 | Pipeline de packs, validador, catálogo, procedencia | — |
| QA con banco | 1–2 | Regresión hardware, matriz de cobertura, casos de fallo | Debe tener autoridad para bloquear un release |
| Release / DevOps | 0.5 | Firma, auto-update, CI, telemetría opt-in | — |
| Legal / cumplimiento | 0.5 | Uso aceptable, consentimiento, datos personales, licencias, procedencia | **No lo asumas como "lo ve el abogado al final"** |

**Pico: 8–10 FTE.** Si tu equipo actual no cubre el perfil HAL, es la primera contratación o la primera alianza a resolver.

---

## 15. Roadmap

**Horizonte total: 26–32 meses** hasta plataforma madura. La v2.0 amplía el horizonte porque añade trabajo real
(diagnóstico y triage, ciclo de aprendizaje, capa de taller). Todo lo que se prometa por debajo de 12 meses es humo.

### 15.1 Núcleo (M0–M11)

| Hito | Duración | Entregable | KPI |
|---|---|---|---|
| **M0 — Spikes de viabilidad** | 4–6 sem | Prototipos reales de Sahara, BROM, FDL, EUB, DFU, fastboot + lectura de GPT. Banco instrumentado mínimo. **Se fijan los esquemas de datos de §20.2 y el diseño multi-puesto de §21.1** | **4 mecanismos** hablan con dispositivo real |
| **M1 — Plataforma base** | 5–6 sem | Arquitectura hexagonal, instalador firmado, `DeviceHost`, journal + evidencia, Plugin SDK v0, licencias v0. **HSM/raíz offline de firma, verificación de calidad de enlace, esquema `OutcomeRecord`, puerto `IInstrument`** | Instala/desinstala limpio; enlace verificado antes de cada escritura |
| **M2 — Identificación universal** | 5–6 sem | Escalera L0–L6, Signature DB v0, motor de cobertura, **puntaje de riesgo de brick v0**, modo simulador básico | **≥85%** del banco identificado a nivel modelo |
| **M3 — Diagnóstico T1 + triage** | 6–7 sem | Suite funcional completa, **árboles de decisión por síntoma (§19.2)**, veredicto de triage, **bundle de soporte + modo rescate** | **≥75%** con veredicto de triage software/hardware |
| **M4 — Correlación, restricciones, backup y Safety Gates** | 5–6 sem | Etapas 5–9 completas y auditables, **procedencia y autenticidad de firmware (§20.4)**, riesgo de brick integrado en el Gate 9 | **100%** de sesiones con backup verificado antes de escribir |
| **M5 — Recuperación de arranque por chipset (T2/T3)** | 8–10 sem | MTK BROM, Qualcomm EDL, motor GPT, backup/restore de particiones, reanudación, **banco de inyección de fallos (§21.10)** | **≥50%** del banco recuperable; 20 flasheos con cortes inyectados sin brick |
| **M6 — Content Platform** | 4–5 sem | Packs firmados, canales, servidor de licencias/contenido, telemetría opt-in + agregados anonimizados | Actualizar cobertura **sin** release de app |
| **M7 — Plugins OEM LATAM** | 6–8 sem | Samsung, Xiaomi, Motorola, Tecno/Infinix/itel, ZTE | **≥70%** del banco recuperable |
| **M8 — T4 recuperación de datos Android** | 5–6 sem | Lectura de `userdata`, extracción, imagen forense, matriz de límites por cifrado, **sesión auditada y prueba de no-acceso (§21.3)** | Recuperación en **≥50%** de casos con userdata intacta y sin cifrado |
| **M9 — Apple** | 5–6 sem | DFU, restore firmado, backup/restore, diagnóstico, extracción desde backup | Restore a versión firmada; **0** funciones de bypass |
| **M10 — Negocio y evidencia** | 6–7 sem | Órdenes, autorizaciones, RBAC, firma del cliente, verificación de propiedad, **modo mostrador + WhatsApp + certificado de reparación (§21.2–21.4)** | Flujo extremo a extremo firmado |
| **M11 — Release y escala** | 4 sem | Auto-update, canal beta/stable, piloto en talleres, marketplace de packs, CI HIL en producción | 30 días en piloto sin regresión crítica |

### 15.2 Fase 2 (tras el lanzamiento)

| Hito | Duración | Entregable | KPI |
|---|---|---|---|
| **F1 — Diagnóstico instrumental** | 5–6 sem | Integración SCPI: fuente de alimentación programable + medidor USB, firma de consumo en arranque, triage electromecánico | **≥60%** de casos "no enciende" clasificados correctamente |
| **F2 — Operación de taller completa** | 6–8 sem | Multi-puesto concurrente, inventario y repuestos, BI del taller, modo lote/refurb, API/POS | Taller de 5 puestos operando en producción |
| **F3 — Expansión de objetivo** | 6–8 sem | Tablets, wearables, TWS y accesorios (§22) | **≥80%** del banco extendido identificado |

**Orden deliberado:** identificación → triage → seguridad/evidencia → recuperación → contenido → OEMs → datos → Apple → negocio → instrumental → operación → expansión.
La razón: puedes lanzar y cobrar en M4 con un producto que **identifica, diagnostica, documenta y respalda sin riesgo**, y crecer hacia la reparación con cobertura medible. Lanzar en M7 con todo prometido y nada verificado es la forma de fracasar.

**Nota de secuencia:** F1–F3 son posteriores al lanzamiento, pero **su diseño de datos y sus puertos se definen en M0–M1** (`IInstrument`, `bench_id`, `OutcomeRecord`). Son las tres cosas que no se pueden retrofitear barato.

---

## 16. Riesgos

| Riesgo | Severidad | Mitigación |
|---|---|---|
| **Dependencia de loaders/auth files legítimos** | Muy alta | Solo fuentes autorizadas; procedencia documentada por archivo; matriz de cobertura honesta; alianzas con proveedores de contenido |
| **Brick del equipo del cliente** | Muy alta | Punto Único de Escritura, backup verificado obligatorio, gates, UPS, reanudación, rollback, informe de causa raíz |
| **Exposición legal de la categoría** | Muy alta | No-alcance en código, verificación de propiedad, consentimiento firmado, asesoría legal formal por país |
| **Suscripción disfrazada de licencia perpetua** (percepción del cliente) | Alta | Comunicar con transparencia versión vs ventana vs Content Pass; el taller debe poder operar sin internet indefinidamente |
| **Antivirus / SmartScreen** | Alta | Firma EV, reputación, submissions a vendors, canal alternativo de instalación |
| **Coste perpetuo del catálogo** | Alta | Content Pass + módulos + licencias de cadena; nunca vender perpetuo "todo incluido para siempre" |
| **Complejidad de Android moderno** (A/B, dynamic partitions, `super`, AVB, virtual A/B, GKI) | Alta | Motor GPT + soporte de `super`/AVB desde el diseño, no como parche posterior |
| **Expectativas irreales del taller en T4** (cifrado) | Alta | Matriz de límites visible y firmada por el cliente antes de aceptar el trabajo |
| **Scope creep** | Alta | Criterio único de admisión de cualquier función: ¿sube el % de cobertura del banco? |
| **Dependencia de una sola persona clave** (HAL) | Media-alta | Documentación de protocolos, banco como fuente de verdad, segunda persona formada en HAL desde M0 |
| **Fuga o compromiso de la clave de firma** | Muy alta | HSM / raíz offline, rotación de claves, doble control, lista de revocación firmada (§21.10) |
| **Coste de soporte desbordado** | Alta | Bundle de soporte con un clic, base de incidencias conocidas, modo rescate, modo ingeniero remoto (§21.10) |
| **Sesiones concurrentes mal soportadas** | Media-alta | `bench_id` desde M1, bloqueo por dispositivo, cola priorizada, límite por bus USB (§21.1) |
| **Expectativa del taller por encima de lo que el software puede diagnosticar** | Alta | Veredicto de triage con confianza explícita e hipótesis descartadas (§19); instrumental en F1 |

---

## 17. Próximos 30 días (M0 en detalle)

| Semana | Acciones |
|---|---|
| **1** | Cerrar decisiones abiertas (§18). Inventariar el banco: fichar cada equipo (SoC, mecanismo accesible, estado, casos). Instalar el hub conmutado y el UPS. Definir el repo y el esqueleto de carpetas. |
| **2** | Spike Sahara: handshake + lectura de GPT en un Qualcomm del banco. Spike BROM: handshake + lectura de chip ID y GPT en un MediaTek. Medir tiempos y tasas de éxito. |
| **3** | Spike FDL (Unisoc) y EUB (Exynos). Spike DFU + restore firmado en iPhone. Documentar cada protocolo: puntos de entrada, handshakes, códigos de error, casos de fallo. |
| **4** | Spike fastboot/ADB + lectura de `build.prop`. **Informe de viabilidad**: qué mecanismos funcionan, con qué restricciones reales, y la primera versión de la matriz de cobertura con el banco real. Decisión go/no-go sobre el alcance de M1. |

**Salida de M0:** una matriz de cobertura real (no teórica) de tus equipos, con números de éxito medidos, y la confirmación de que los 4 mecanismos principales son alcanzables con material legítimo. Si algún mecanismo no es alcanzable, se descubre en 30 días, no en el mes 12.

**Adicionalmente, M0 debe cerrar las decisiones que no se pueden retrofitear barato:** (a) el esquema `OutcomeRecord` (§20.2); (b) `bench_id` y el modelo de bloqueo por dispositivo para soporte multi-puesto (§21.1); (c) el puerto `IInstrument` (§19.4), aunque todavía no haya instrumentos conectados; (d) el esquema de la base de conocimiento de síntomas (§19.5). Ninguna de las cuatro se **implementa** en M0: solo se **fijan sus contratos**. Retrofittearlas en el mes 18 cuesta meses; fijarlas ahora cuesta días.

---

## 18. Decisiones todavía abiertas

**Críticas (bloquean M0):**

1. **Ventana de actualizaciones y Content Pass** (§10): ¿aceptas el modelo perpetuo + ventana + pack anual, o prefieres perpetuo con packs comprados por separado?
2. **Stack:** ¿tu equipo es .NET o web/TS? Esto decide WinUI/WPF/Avalonia vs Tauri 2 (ver §24 de UX/UI).
3. **Países de operación** en el primer año (define qué normativa hay que preparar primero: MX, BR, AR, CO, CL) y qué idiomas del informe al cliente (ES/PT).

**De producto:**

4. **Nombre real del producto** (hoy es un placeholder en clave).
5. **Módulo forense/pericial:** ¿se incluye como línea de negocio desde M10, o se deja para después del lanzamiento?
6. **Apple:** ¿confirmas el alcance honesto de §7 (restore firmado + backup + datos con credenciales, sin bypass) como definición cerrada del módulo?
7. **Diagnóstico de hardware (§19):** ¿entra el triage por síntomas en el alcance del v1 (M3), o solo la suite funcional?
8. **Sesión auditada / prueba de no-acceso (§21.3):** ¿se prioriza junto con T4, o antes?

**De operación y futuro:**

9. **Instrumentación (§19.4):** ¿ya usan fuente de alimentación programable y medidor USB en el taller? ¿Marca/modelos? (define si el adaptador SCPI entra en F1 o se adelanta)
10. **Multi-puesto (§21.1):** ¿cuántas mesas simultáneas debe soportar el taller objetivo en el año 1?
11. **Avisos al cliente (§21.2):** ¿WhatsApp Business API, SMS local, o solo impresión? (en LATAM, WhatsApp es el canal real)
12. **Expansión (§22):** ¿tablets primero, o wearables/TWS? (define el orden de F3)
13. **Modo lote/refurb:** ¿es parte del mercado objetivo desde el inicio o se deja a F2?

---

## 19. Diagnóstico y triage (hardware + síntoma)

> Añadido en v2.0. Sin este bloque, la plataforma diagnostica software dentro del sistema operativo,
> que no es el problema real del taller.

### 19.1 Principio

La primera pregunta del taller no es «¿qué firmware tiene?» sino **«¿es software o hardware?»**. La plataforma
emite un **veredicto de triage** — `Software` / `Hardware` / `Indeterminado` — con grado de confianza, hipótesis
descartadas y pruebas realizadas, **antes** de proponer cualquier reparación. Ese veredicto es también lo que
justifica el presupuesto ante el cliente.

### 19.2 Árboles de decisión por síntoma

| Síntoma reportado | Hipótesis a separar | Pruebas que las separan |
|---|---|---|
| No enciende | Batería agotada / batería dañada / circuito de carga / PMIC / boot corrupto / pantalla muerta con equipo vivo | Firma de consumo en arranque, voltaje bajo carga, ¿responde el bootloader?, backlight |
| Enciende pero no muestra (suena, vibra) | Panel / flex / GPU / driver | Consumo normal + backlight + test de panel y de GPU |
| Se reinicia solo | Batería con resistencia interna alta / térmico / kernel panic / almacenamiento degradado | Curva de consumo bajo carga, temperatura, logs de panic |
| No carga | Cable o puerto / flex / controlador de carga | Corriente negociada (USB-PD/QC), consumo en reposo |
| Batería dura poco | Batería degradada / consumo anómalo de software | Ciclos, capacidad, resistencia interna, consumo en reposo |
| No da señal | Software / EFS-NV / antena | Estado de radio, bandas, IMEI, logs de `modem` |
| Se queda en logo | Boot o partición corrupta / almacenamiento | Hash de particiones, pruebas de lectura, error de montaje |
| No lo reconoce la PC | Modo / driver / puerto / cable | Enumeración USB, diagnóstico de driver, puerto alterno |

### 19.3 Pruebas no invasivas

- Salud de batería: ciclos, capacidad estimada, resistencia interna, caída de voltaje bajo carga.
- Circuito de carga: corriente de entrada y salida, estabilidad, negociación USB.
- **Firma de consumo en arranque**: el patrón de corriente distingue PMIC / CPU / display / almacenamiento.
  Es la prueba más discriminante para «no enciende».
- Comportamiento térmico bajo carga sostenida.
- Suite funcional: panel (píxeles muertos, uniformidad), touch (multitáctil, zonas muertas), cámaras, audio
  (altavoz, auricular, micrófono, loopback), sensores, radios (Wi-Fi, BT, GNSS, NFC), botones, vibración, biometría.
- Almacenamiento: salud de eMMC/UFS, sectores ilegibles, verificación de escritura.
- Arranque real por etapas: ¿responde el bootloader? ¿arranca el kernel? ¿llega a UI?

### 19.4 Instrumentación de laboratorio (puerto `IInstrument`)

Contrato único para instrumentos controlables por software (SCPI sobre serial, USB o TCP):

| Instrumento | Uso en el flujo | Etapas |
|---|---|---|
| Fuente de alimentación programable | Alimentar la placa sin batería, medir consumo en reposo/boot/carga, detectar cortos | 4, 10, 11 |
| Medidor de USB / analizador de carga | Verificar negociación y estabilidad de carga | 4, 11 |
| Multímetro o analizador (futuro) | Continuidad, voltajes de riel | 4 |

**Reglas:** las *lecturas* son evidencia (timestamp + hash); la *escritura* sobre el instrumento (fijar voltaje,
cortar alimentación) se trata como acción de riesgo y pasa por el mismo pipeline de gates. El instrumental es
opcional: la plataforma **degrada con elegancia** si no está presente.

### 19.5 Base de conocimiento de síntomas

No es una wiki: es un grafo versionado y firmado (Knowledge Pack) de
`síntoma → hipótesis → prueba → resultado esperado → confianza → acción recomendada → coste estimado`.
Se enriquece con los resultados reales de §20 y con aportaciones de técnicos, con **curaduría** (un técnico no
puede degradar la KB de todos).

### 19.6 Salida: `TriageReport`

Veredicto + confianza + pruebas realizadas + hipótesis descartadas y por qué + acción recomendada + piezas
probables + estimación de tiempo. Es el documento con el que el mostrador cotiza.

---

## 20. Ciclo de aprendizaje y telemetría de resultados

### 20.1 El bucle

`sesión → resultado registrado → agregado anonimizado → predicción (riesgo, correlación) → mejor decisión en la siguiente sesión`.

Sin este bucle, cada sesión muere en el taller y la plataforma nunca aprende. Con él, la plataforma vale más cada
mes que pasa: es lo único del plan que **se revaloriza solo**.

### 20.2 Esquema `OutcomeRecord` (contrato fijado en M0, implementado en M2)

`session_id` · `bench_id` · `fingerprint_hash` · `síntoma` (código de taxonomía) · `diagnóstico` · `plan_id` +
versión de packs · `acciones[]` con parámetros · `resultado` (éxito / parcial / fallo) · `causa_raíz` · `intentos` ·
duración por etapa · `incidentes de enlace` · `procedencia del firmware usado`.

**Sin datos personales:** identificadores hasheados, nada del cliente, nada del contenido de `userdata`.

### 20.3 Puntaje de riesgo de brick

`(similitud de dispositivo × frecuencia histórica de fallo × tamaño de muestra × procedencia del material × señales de alerta del equipo)`
→ semáforo + porcentaje + explicación en lenguaje llano. Se integra en el **Gate 9**: por encima de un umbral
configurable exige aprobación de supervisor. Es el antídoto contra el peor escenario del negocio (brickear el
equipo del cliente).

### 20.4 Procedencia y autenticidad de firmware

| Nivel | Significado |
|---|---|
| **Oficial firmado** | Verificado contra la firma del fabricante |
| **Oficial no firmado** | Origen conocido, sin firma verificable |
| **Verificado por hash** | Coincide con un hash bueno conocido del catálogo |
| **Desconocido** | Sin procedencia → advertencia explícita, solo con confirmación |
| **Alterado / sospechoso** | Firmas internas incoherentes, binarios inyectados, metadatos falsos → **bloqueo** |

Verificaciones: coherencia de **variante regional / CSC** (la causa número uno de bricks por flashear el CSC
equivocado), coherencia de build con el modelo, integridad de metadatos y detección de contenido inyectado.

**Por qué vende:** los talleres de LATAM descargan firmware de sitios dudosos todos los días. Una plataforma que
dictamina «auténtico / alterado / no corresponde a tu variante» ataca la mayor causa de bricks y de malware, y hoy
nadie lo hace bien.

### 20.5 Imágenes de rescate verificadas

Con consentimiento y sin datos personales, cada backup de particiones críticas (etapa 7) puede convertirse en una
**imagen de rescate verificada** del banco. Con el tiempo se forma un catálogo de imágenes buenas por modelo y
variante: otro efecto de red, y un argumento de venta («rescate inmediato para X modelos»).

### 20.6 Privacidad y ética de los datos

Opt-in explícito · **local-first** (el taller ve el valor antes de compartir) · identificadores hasheados ·
agregación con **k-anonimato** (nunca mostrar una estadística basada en menos de N casos) · derecho a no participar
sin perder funcionalidad · los datos del taller son del taller: exportables y borrables. Cumple LFPDPPP / LGPD /
Ley 25.326 sin trabajo extra.

### 20.7 Valor comercial

Es el activo que sostiene el **Content Pass** (§10), permite vender «cobertura verificada» a cadenas y aseguradoras,
y hace defendible el puntaje de riesgo. La competencia lleva años acumulándolo: es el foso más difícil de cruzar y
el más caro de improvisar.

---

## 21. Operación de taller y robustez de ingeniería

### 21.1 Multi-puesto concurrente

Un taller de 3–5 mesas trabaja en paralelo; el plan v1.0 asumía sesión única y eso es una limitación grave.
**Toda entidad lleva `bench_id` desde M1.** Modelo: bloqueo optimista por dispositivo, cola priorizada por orden
de servicio, reparto de ancho de banda USB, límite de sesiones por hub y advertencia si dos sesiones comparten bus
y degradan el enlace.

### 21.2 Modo mostrador

Recepción del equipo → verificación de propiedad (§11) → consentimiento digital con firma en pantalla →
comprobante impreso → **etiqueta con código de barras** para la bolsa (lectura por escáner en cada etapa) →
aviso al cliente por WhatsApp/SMS cuando esté listo → entrega con certificado. Es lo que hace que el taller use la
plataforma desde la primera milla, no solo el técnico en el banco.

### 21.3 Sesión auditada y prueba de no-acceso

El miedo real del taller es que lo acusen de robar fotos. La plataforma registra y **demuestra** que no abrió
particiones de datos: manifiesto verificable con hashes de lo leído y constancia de que `userdata` no se montó
salvo autorización explícita. Muy valioso en LATAM y prácticamente inexistente en la competencia.

### 21.4 Certificado de reparación firmado

Fingerprint · qué se escribió (hashes) · resultado de verificación · **IMEI intacto comparado con el registrado en la
recepción** · pruebas funcionales post · firma del taller. Sirve para dar garantía con respaldo y para desactivar
reclamaciones y acusaciones.

### 21.5 Inventario y compatibilidad de repuestos

La plataforma conoce el board ID (escalera L5): puede sugerir piezas compatibles y cruzar con stock. Convierte el
diagnóstico en una lista de compra.

### 21.6 BI del taller

Éxito por técnico/modelo/acción, tiempo medio por tipo de reparación, reincidencias, margen, equipos con más
fallos. Es lo que compra el dueño, no el técnico.

### 21.7 Modo lote / refurb

N dispositivos, mismo plan, reporte individual, tolerancia a fallos parciales y **parada automática si el ratio de
fallo excede un umbral** (protege el lote completo). Mercado real en LATAM: empresas que reacondicionan lotes de
usados.

### 21.8 API e integración con POS

REST local + webhooks + exportación. No obligar al taller a cambiar de sistema.

### 21.9 Modo simulador / entrenamiento

Reproducción de sesiones grabadas, dispositivos simulados, certificación interna de técnicos y demo comercial sin
riesgo. Reduce soporte y acelera la venta.

### 21.10 Robustez de ingeniería (se decide hoy, no en el mes 18)

| # | Punto | Por qué | Cuándo |
|---|---|---|---|
| 1 | **Verificación de calidad de enlace antes de escribir** (throughput, reintentos, errores USB, timeout de escritura) | El cable/hub barato es la causa principal de interrupciones a mitad de flasheo; abortar antes de escribir elimina una fracción enorme de bricks | M1 |
| 2 | **Banco de inyección de fallos**: corte de energía por puerto, reset USB, enlace degradado, corrupción de datos | La reanudación no se diseña, se prueba rompiendo; sin esto «reanudable» es una hipótesis | M5 |
| 3 | **HSM o raíz offline de firma** + rotación de claves + lista de revocación firmada | Una clave filtrada permite distribuir packs maliciosos a todos los talleres: riesgo existencial | M1 |
| 4 | **Soporte remoto**: bundle con un clic (journal + entorno + versiones, redactado), base de incidencias conocidas, modo ingeniero remoto | Con una herramienta capaz de brickear, el coste de soporte se dispara si no está diseñado | M3 |
| 5 | **Modo rescate desde el journal** | Recuperar una sesión fallida es el caso de uso más valioso del producto | M3 |
| 6 | **Offline total real**: packs importables por USB, licencia verificable offline, gracia amplia, actualización sin internet | Internet inestable es la norma en el taller LATAM | M1–M6 |
| 7 | **Modelo de amenazas de la propia herramienta**: crackeo de licencia, uso indebido, exfiltración de datos de clientes | Se diseña, no se improvisa | M1 |

---

## 22. Expansión del objetivo

La arquitectura es **por mecanismo, no por producto**, así que extender es barato. Orden recomendado:

| Orden | Categoría | Mecanismos reutilizados | Atractivo LATAM |
|---|---|---|---|
| 1 | **Tablets** (Android e iPad) | Todo el stack: mismos BROM / EDL / DFU | Muy alto (volumen educativo y familiar) |
| 2 | **Wearables** (smartwatch MTK/QC) | BROM / EDL, DFU para Apple Watch | Medio-alto, poca competencia |
| 3 | **TWS y accesorios** | Protocolos de SoC de audio, USB | Alto volumen, reparación poco atendida |
| 4 | **Android TV, cajas y sticks** | EDL / BROM, fastboot | Medio |
| 5 | **Módems, routers y POS** | Serial, fastboot, firmware firmado | Nicho B2B de alto valor |

Criterio: no se abre una categoría hasta que la anterior tenga cobertura medida en el banco.

---

## 23. Priorización y regla de admisión

**Regla de admisión única.** Una función entra solo si cumple al menos una de estas tres condiciones:
**(a)** sube el % de cobertura del banco, **(b)** reduce la tasa de bricks, **(c)** aumenta lo que el taller puede cobrar.

| Entra ahora (retrofit caro) | Fase 2 | Nunca |
|---|---|---|
| Esquema `OutcomeRecord` (M0/M1) | Instrumentación SCPI completa (§19.4 → F1) | Cualquier forma de bypass de bloqueos |
| `bench_id` + diseño multi-puesto (M1) | Sesión auditada avanzada, modo lote/refurb (F2) | Dependencia de la nube para operar |
| Verificación de calidad de enlace (M1) | Inventario, BI, API/POS (F2) | Caja o dongle propio como único modo de licencia |
| HSM / raíz offline de firma (M1) | KB de síntomas colaborativa | Marketplace abierto sin curaduría |
| Riesgo de brick v0 (M2) | Tablets, wearables, TWS (F3) | Portable a macOS/Linux antes de tener mercado |
| Procedencia de firmware (M4) | | Herramientas propias de hardware más allá de accesorios |
| Bundle de soporte + modo rescate (M3) | | |
| Modo simulador (M2) | | |

**Coste de retrofit:** lo que está en «entra ahora» cuesta días si se decide en M0 y meses si se decide en el mes 18.
Ese es el único motivo por el que figura en M0 aunque se implemente mucho más tarde.

---

## 24. UX/UI: qué stack de interfaz usar

> Investigación sobre GitHub, 21st.dev, Figma y documentación de fabricantes. La pregunta no es «qué se ve bonito»
sino **qué aguanta un taller real**: pantallas densas en datos, operaciones de 30 minutos, PCs modestas,
técnicos de pie en un banco y ventanas de 1366×768.

### 24.1 Restricciones que la UI debe soportar (y que descartan opciones)

| Restricción | Consecuencia de diseño |
|---|---|
| El flujo son **13 etapas**, no un menú de herramientas | Una pantalla principal de sesión tipo *stepper* con contexto, no 40 pestañas sueltas |
| Operaciones de **20–60 minutos** con riesgo de corte | Progreso honesto (bytes, ETA, reintentos), estado del enlace siempre visible, botón de reanudar, indicador explícito de «se puede desconectar» |
| Datos muy densos: tabla de particiones, log en streaming, árbol de dispositivos, curvas I-V | Necesita **DataGrid/TreeGrid virtualizado, panel de docking y visor de log**: es el punto donde la mayoría de librerías «bonitas» se rompen |
| Uso **de pie, a veces con guantes**, escáner de códigos de barras, segundo monitor | Navegación 100% por teclado, atajos por etapa, objetivos de clic grandes, sin interacciones solo-hover, modo «a distancia» |
| PC de taller: Win10, 4–8 GB RAM, disco mecánico, gráficos integrados | Presupuesto de rendimiento estricto; virtualización de todo; nunca bloquear el hilo de UI en I/O de USB |
| Acción destructiva = posible brick | Rojo **reservado** para riesgo; confirmaciones que exigen intención deliberada (mantener pulsado o escribir el modelo) |
| Offline, ES/PT, multi-puesto, licencia perpetua | Sin pantallas bloqueadas por servidor; i18n desde el día 1; estado de licencia y packs visible en la barra de estado |

### 24.2 Opciones evaluadas

| Opción | Evidencia / estado | A favor | En contra |
|---|---|---|---|
| **WinUI 3 + Windows App SDK** ([docs](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)) | Framework nativo recomendado por Microsoft para apps nuevas de escritorio; Fluent integrado | Nativo, Fluent 1:1, futuro oficial | Empaquetado con fricción, ecosistema de controles complejos más joven, menos ejemplos para apps densas en datos |
| **WPF + WPF UI de lepoco** ([GitHub](https://github.com/lepoco/wpfui)) | **9.6k estrellas, MIT**, ~850k descargas, Fluent para WPF, galería en Microsoft Store | WPF es el framework más probado para apps de datos intensivas; shell Fluent gratis; MIT | Librería comunitaria: faltan algunos controles y hay reportes de conflictos de theming en apps grandes |
| **WPF/Avalonia + suite comercial** (DevExpress, [Telerik](https://www.telerik.com/products/wpf/overview.aspx), Syncfusion) | Telerik UI for WPF: **165+ controles**, 20+ temas, **UI kits para Figma**, compatibilidad con Avalonia XPF | Resuelve «más completa» de inmediato: TreeGrid, docking, charts, scheduler, visor de log; diseño→código ya mapeado | Coste de licencia; dependencia de proveedor; riesgo de UI «genérica de suite» si no se thematiza |
| **Avalonia 11/12 + SukiUI / FluentAvalonia / ShadUI** ([SukiUI](https://github.com/kikipoulet/SukiUI) 2.6k★ MIT, [FluentAvalonia](https://github.com/amwx/FluentAvalonia) 1.6k★ MIT, [ShadUI](https://github.com/accntech/shad-ui) 553★ MIT) | Avalonia: MIT, MIT-licensed, 31k★, ~410M compilaciones en el 1S-2026, [DevTools, TreeDataGrid y soporte comercial](https://avaloniaui.net/avalonia/windows) | Control total del theming, mismo XAML/WPF mental model, futuro Linux, librerías inspiradas en shadcn | Ecosistema de controles de datos menos maduro que WPF+comercial; SukiUI ya está en modo mantenimiento |
| **Tauri 2 + React + shadcn/ui + [21st.dev](https://21st.dev/)** | 21st.dev: **12.000+ componentes** React/Tailwind/shadcn, se instalan con el CLI de shadcn (`npx @21st-dev/cli init`), el código queda en tu repo sin lock-in; **244 registries** indexados en su [directorio shadcn](https://21st.dev/community/shadcn-directory) | La ruta diseño→código más madura del mercado; Figma kit oficial de shadcn ([ui.shadcn.com/docs/figma](https://ui.shadcn.com/docs/figma)); instalador muy pequeño | Dos lenguajes; para tabla de particiones y log masivo necesitas TanStack Table o AG Grid; el HAL en Rust añade curva de aprendizaje |

**Referencias de diseño (no de código):** [Fluent 2 de Microsoft](https://fluent2.microsoft.design/get-started/design) (lenguaje + kits Figma por plataforma), **IBM Carbon** (el mejor sistema para consolas técnicas densas), [Untitled UI](https://www.figma.com/community/file/1020079203222518115/untitled-ui-free-figma-ui-kit-and-design-system-v2-0) (kit gratuito, muy completo para dashboards), y la colección de [UI kits de dashboard de Figma](https://www.figma.com/community/ui-kits/dashboards).

### 24.3 Recomendación

**Ruta A — DECIDIDA (ver ADR-001, §25):**
> **WPF sobre .NET 10 LTS + DevExpress WPF como única suite de controles y único motor de tema**, con los
> tokens del §24 convertidos en tema propio de DevExpress.

Por qué: es la combinación que ya resolvió «app de escritorio densa en datos con hardware» en cientos de productos
industriales; el núcleo duro (grids, docking, informes) es exactamente donde las librerías gratuitas se quedan
cortas; y **el informe del cliente es una función central del producto**, que XtraReports resuelve de fábrica.
El coste de licencia es trivial frente al coste de construir a mano un TreeGrid virtualizado **y** un motor de
informes con plantillas, logo del taller y exportación PDF.

**Ruta B — descartada (ADR-001).** Tauri 2 + React + shadcn/ui + 21st.dev habría metido dos runtimes y dos
lenguajes en el punto más crítico del producto: el HAL.

**Ruta C — descartada por ahora (ADR-001).** Avalonia + FluentAvalonia/SukiUI se reevaluará si algún día el
producto necesita Linux o macOS; hoy el objetivo es Windows.

**Regla:** no mezclar dos runtimes de UI (ni Electron). Decisión cerrada el 2026-09-25: **Ruta A** (ver ADR-001).
No mezclar tampoco dos motores de tema (ver ADR-003).

### 24.4 Principios de UX para esta herramienta

1. **La UI *es* el pipeline de 13 etapas.** Una pantalla de sesión principal: etapa actual expandida, etapas
   completadas colapsadas con su evidencia, y siempre visible **qué falta y por qué**. Nunca un menú de
   herramientas inconexas.
2. **Dos modos, una app.** *Modo taller* (guiado: la plataforma propone el siguiente paso) y *modo ingeniero*
   (control total con consola de protocolo). El mismo binario, distinto nivel de exposición.
3. **El rojo es sagrado.** Reservado exclusivamente para «esto puede brickear el equipo». Si el rojo se usa para
   cerrar sesión, el técnico deja de verlo.
4. **Estado del enlace, permanente.** Un indicador en la barra de estado con calidad del enlace, errores USB,
   throughput y una respuesta clara a «¿puedo desconectar?». Es la información ambiental más valiosa de la app.
5. **Consola siempre disponible, nunca obligatoria.** Log en streaming con filtros, búsqueda y «copiar bundle de
   soporte». Los técnicos avanzados viven ahí; los nuevos no deben pisarla.
6. **Todo es evidencia.** Cada panel tiene «guardar en evidencia» a un clic. Si el informe se diseña al final, sale
   un collage; si cada vista exporta, sale un informe.
7. **Progreso honesto.** Bytes, ETA, paso actual, reintentos y — obligatorio — **qué pasa si se corta ahora**, con el
   camino de rollback a la vista antes de empezar.
8. **Confirmación proporcional al riesgo.** Mantener pulsado, escribir el modelo o firma de supervisor. Nunca un
   simple «¿Estás seguro?» que se aprende a clicar por reflejo.
9. **Ergonomía de banco:** atajos para las 13 etapas, objetivos grandes, nada que dependa del hover, escáner de
   códigos de barras como entrada de primer nivel, y un «modo banco» que agranda la tipografía y atenúa el resto.
10. **Presupuesto de rendimiento escrito:** arranque < 3 s en la PC objetivo, UI nunca bloqueada durante I/O USB,
    buffer de log acotado, evidencias con carga diferida. Las PCs de taller son malas: diseñar para eso.
11. **Offline como estado normal, no como error.** Sin pantallas que esperan a un servidor; licencia y packs con
    estado visible y verificable sin internet.
12. **El informe es la cara del producto.** Es lo único que ve el cliente final: lenguaje llano, A4, logo del taller,
    sin jerga técnica innecesaria, y con el veredicto de triage arriba.

### 24.5 Referencias a estudiar (buenas y malas)

- **Buenas:** Android Studio (device manager + logcat), Visual Studio (docking, output, progress), Linear
  (teclado primero), Carbon de IBM (consolas de datos densos), 3uTools (jerarquía clara de funciones).
- **Malas (lo que hace la competencia):** Odin y UnlockTool — infiernos de modales, cero progreso real, cero
  evidencia, ningún estado de enlace. Son el manual de lo que **no** hay que copiar.

### 24.6 Proceso de diseño recomendado

1. `design/tokens` como **única fuente de verdad** (color, tipografía, espaciado, estados, semántica de riesgo)
   → genera la librería de Figma y el tema en XAML/React.
2. **Prototipo primero de la pantalla de sesión de 13 etapas**, no de todo el dashboard.
3. **Validar con un técnico real durante 2 días en el banco** antes de escribir código de UI (ya tienes personal y banco).
4. Diseñar el **informe** en paralelo al prototipo: define qué datos deben capturarse.
5. Congelar el sistema de diseño al final de M2; después, cambios solo por tokens.

---

## 25. Decisiones de arquitectura (ADR)

Registro vinculante. Una ADR solo cambia si se escribe otra que la sustituya.

### ADR-001 — Framework de UI: **WPF sobre .NET 10 LTS** · Aceptada

**Contexto.** El producto es Windows-only, denso en datos (árbol de particiones, log en streaming, docking,
curvas de instrumentación), con I/O de hardware pesado, impresión/exportación de informes como función central,
y debe correr en PCs de taller modestas (Win10, 4–8 GB RAM).

**Decisión.** WPF sobre .NET 10 LTS.

**Por qué no las alternativas:**

| Alternativa | Motivo del rechazo |
|---|---|
| WinUI 3 / Windows App SDK | Sigue con **vacíos en controles especializados** (árbol, docking, impresión/reportes) y despliegue MSIX-first; para una LOB de ingeniería con equipo pequeño es riesgo sin recompensa. Su ventaja real (compositor a 60 fps, AOT) no es el cuello de botella del producto |
| Avalonia | Buena y MIT, pero el ecosistema de **controles de datos** está por detrás de WPF+comercial, y para un objetivo Windows-only no aporta nada a cambio del riesgo |
| Tauri 2 + React | Metería **dos runtimes y dos lenguajes** en el punto más crítico (el HAL) y añadiría Rust al perfil más escaso del equipo. La ventaja de instalador pequeño no compensa |

**Consecuencias.** WPF está en mantenimiento activo (no deprecado) y es el que tiene el ecosistema más amplio,
impresión/reportes maduros, diseñador XAML estable y soporte en Windows 10. A cambio renunciamos a lo mejor de
WinUI (rendimiento de composición, AOT) que hoy no necesitamos.

### ADR-002 — Suite de controles: **DevExpress WPF** · Aceptada

**Decisión.** DevExpress WPF como suite única para el núcleo duro: TreeList (particiones), Docking (paneles),
Charts (instrumentación), RichText/PDF y **XtraReports** para los informes al cliente, técnico y pericial.

**Razón decisiva.** El informe **es** una función central del producto (§2, etapa 13; §21.4) y XtraReports resuelve
eso de fábrica. Construir a mano un motor de informes con plantillas, logos de taller, exportación PDF e impresión
A4 cuesta más que la licencia. Se añade: DevExpress publica guía oficial de migración a .NET 10 y **SBOM para
cumplimiento (CRA)**, relevante para vender a cadenas.

**Alternativa aceptable:** Telerik UI for WPF (165+ controles, UI kits de Figma, compatibilidad con XPF). Si el
proveedor resulta un problema, es el reemplazo natural.

### ADR-003 — Un solo motor de tema · Aceptada

**Decisión.** Un único motor de tema: el de DevExpress, **generado desde `design/tokens`** (§24).
**WPF UI (lepoco) y el tema Fluent nativo de WPF quedan descartados como motores** — se usan solo como
referencia del lenguaje visual Fluent 2.

**Por qué.** Mezclar dos motores es donde sangran los proyectos: un control que no respeta el tema, un
`ContentPresenter` que se rompe, un color que no cambia en dark mode. Pagar la suite compra **cohesión**, y la
cohesión vale más que el MIT de la librería gratuita.

### ADR-004 — Persistencia: **SQLite + Microsoft.Data.Sqlite + Dapper** · Aceptada

**Decisión.** Sin ORM. `Microsoft.Data.Sqlite` con SQL explícito y un migrador propio.

**Por qué.** El journal es *append-only* y se escribe en cada paso de cada etapa: necesita ser **predecible y
rápido**, no conveniente. Un ORM en el camino crítico del journal es deuda técnica con intereses. El CRUD del
resto (órdenes, clientes, evidencia) no justifica la carga de EF Core en una app de escritorio.

### ADR-005 — Empaquetado: **MSI (WiX v5) + Authenticode EV + auto-update firmado** · Aceptada

Por §12. Sin firma EV, SmartScreen y los antivirus marcan la herramienta como PUA y el taller no puede instalarla.

### ADR-006 — Toolchain obligatorio · Aceptada

**.NET 10 LTS + Visual Studio 2026.** Confirmado: **Visual Studio 2022 no puede apuntar a .NET 10 ni usar C# 14.**
Alternativa válida: VS Code + C# Dev Kit. Esto es una compra de herramientas y una decisión de equipo, no un detalle.

### ADR-007 — Prohibido arrancar sobre .NET 8 o .NET 9 · Aceptada

Ambas llegan a **fin de soporte el 10 de noviembre de 2026** (a seis semanas de la fecha de esta decisión).
Cualquier prototipo que nazca sobre .NET 9 nace con EOL encima. La única versión aceptable es **.NET 10 LTS**
(hasta el 14 de noviembre de 2028).

---

## 26. Liberación de equipos de operador (mercado de importación de EEUU)

> Añadido en v2.1. En México una parte enorme del mercado son equipos importados de Estados Unidos
> bloqueados a AT&T, T-Mobile, Verizon, Metro, Cricket o Boost. Es una línea de negocio real del taller
> y hay que atenderla **sin** entrar en el terreno prohibido.

### 26.1 Lo que la plataforma sí hace

| Capacidad | Cómo |
|---|---|
| **Leer** el estado del bloqueo | Del propio equipo: política del operador y estado de activación |
| **Comprobar elegibilidad** | Requisitos del operador: equipo pagado, contrato cumplido, plazo. Verizon libera a los 60 días; el prepago suele exigir de 6 a 12 meses |
| **Guiar el trámite oficial** | Instrucciones del portal del operador, con el modelo exacto y el número de serie |
| **Verificar** el resultado | Volver a leer el estado tras la liberación y confirmar el cambio |
| **Documentar** | La liberación queda en la orden de servicio con evidencia: el taller puede cobrarla y demostrarla |
| **Advertir de la trampa de la eSIM** | Un iPhone 14, 15 o 16 de EEUU (A2649, A2846, A3081) es **solo eSIM**: no acepta SIM física mexicana. Es la causa número uno de devoluciones en este negocio |

### 26.2 Lo que la plataforma nunca hace (permanente)

Liberar por bypass, por exploitation, mediante servicios de terceros no autorizados o reescribiendo el IMEI;
tampoco trabajar sobre equipos con propiedad no verificada o reportados como robados. La prueba unitaria
`La_plataforma_nunca_libera_por_bypass` existe precisamente para que quitar esta prohibición exija
**borrar una prueba** y no solo cambiar una línea de código sin que nadie se entere.

### 26.3 Por qué es un diferenciador y no una limitación

La competencia (Chimera y similares) vende eliminación de FRP y reparación de IMEI —terreno que este producto
tiene prohibido— y acaba de añadir «Carrier Relock» a su catálogo. Nosotros vendemos lo contrario:
**liberación legítima, verificada y documentada con certificado**. Eso es exactamente lo que compra un taller
con abogado y con seguro, y es lo que el dueño puede enseñar al cliente cuando este dice que le devolvieron
un equipo bloqueado.

### 26.4 Implementación

- **Catálogo** (`docs/catalogo-mx.csv`): 170 modelos del mercado mexicano con las variantes de operador de
  EEUU, su SoC, su placa, su firmware base, su mecanismo y su ruta de liberación (`unlock_path`).
- **Inventario** (`docs/inventario-demo.csv`): el estado observado por equipo (bloqueado, operador, SIM).
- **Domain**: `CarrierLockState`, `UnlockPath`, `SimType` y `UnlockGuidance` — el texto de orientación para el
  técnico, para que la respuesta ante un equipo bloqueado sea siempre la misma y siempre legítima.
- **Veredicto**: un equipo bloqueado añade automáticamente la advertencia de liberación al veredicto de
  cobertura, y un grupo con equipos solo eSIM añade la advertencia de SIM.

---

## Apéndice A — Esquema de carpetas propuesto

```
NAVAJA/
├─ src/
│  ├─ Desktop.App/            (WinUI/WPF/Avalonia — solo presentación)
│  ├─ Desktop.ViewModels/
│  ├─ DesignSystem/           (§24 — tokens, temas, componentes reutilizables)
│  ├─ UseCases/               (orquestación de las 13 etapas; se llama UseCases y no Application
│  │                           porque un namespace «Application» colisiona con System.Windows.Application
│  │                           y produce CS0118 en cualquier archivo WPF)
│  ├─ Domain/                 (CERO I/O — modelo, reglas, cobertura, evidencia)
│  ├─ Domain/Inventory/       (§26 — catálogo, cobertura medida, bloqueo de operador)
│  ├─ Cli/                    (autocomprobación de invariantes y matriz de cobertura)
│  ├─ Ports/                  (interfaces de adaptadores: ITransport, IInstrument…)
│  ├─ Identification/         (escalera L0–L6, resolución, Signature DB)
│  ├─ Diagnostics/            (suite funcional)
│  ├─ Diagnostics.Hardware/   (§19 — árboles por síntoma, firma de consumo)
│  ├─ Instruments/            (§19.4 — adaptadores SCPI)
│  ├─ SymptomKb/              (§19.5 — base de conocimiento de síntomas)
│  ├─ Learning/               (§20 — OutcomeRecord, agregados, riesgo de brick)
│  ├─ FirmwareProvenance/     (§20.4 — autenticidad y variante regional)
│  ├─ PartitionEngine/        (motor genérico dirigido por GPT, agnóstico de OEM)
│  ├─ SafetyGates/            (pipeline transversal + emisión de plan firmado)
│  ├─ Plugins.Sdk/            (contratos, manifiesto, sandbox, ABI)
│  ├─ Plugins.Oem.*/          (Samsung, Xiaomi, Motorola, Tecno, ZTE…)
│  ├─ Adapters.Protocols/     (ADB, Fastboot, EDL, BROM, FDL, EUB, DFU, muxd)
│  ├─ Adapters.Windows/       (USB/WinUSB, Serial, procesos, drivers, DPAPI)
│  ├─ DeviceHost/             (proceso privilegiado + named pipe)
│  ├─ Content/                (carga, validación, firma y versionado de packs)
│  ├─ Licensing/              (activación offline, Ed25519, binding)
│  ├─ Workshop/               (§21 — multi-puesto, mostrador, certificados)
│  ├─ Reporting/              (informe cliente/técnico, BI)
│  ├─ Support/                (§21.10 — bundle de soporte, modo rescate)
│  └─ Persistence/            (SQLite, migraciones, repositorios)
├─ packs/                     (Knowledge Packs firmados — por canal)
├─ tools/
│  ├─ PackValidator/          (validador en CI)
│  ├─ FaultInjector/          (§21.10 — cortes, reset USB, enlace degradado)
│  └─ CoverageMatrix/         (genera la matriz de cobertura desde el banco)
├─ tests/
│  ├─ Unit/                   (dominio puro, sin hardware)
│  ├─ Integration/
│  └─ DeviceFarm/             (suite de regresión hardware-in-the-loop)
├─ design/                    (§24 — design system, tokens, Figma, librería UI)
├─ docs/                      (este plan + manuales + matriz de cobertura)
└─ installer/                 (WiX v5, firma, auto-update)
```

## Apéndice B — Contratos clave (esbozo)

```csharp
// Manifiesto de plugin — firmado, versionado, con permisos declarados
public sealed record AdapterManifest(
    string Id, Version Version, string AbiVersion, string Author,
    IReadOnlySet<Capability> Capabilities,
    WriteScope DeclaredWriteScope,      // None | Partitions | Full — auditado
    string Signature);

public interface IDeviceAdapter {
    AdapterManifest Manifest { get; }
    ValueTask<ProbeResult> ProbeAsync(TransportRef transport, CancellationToken ct);
    DeviceFingerprint ReadFingerprint(ProbeResult probe);
}

// El motor de reparación SOLO acepta este token: lo emite el Safety Gate
public sealed record SignedRepairPlan(
    RepairPlan Plan,                    // pasos, fuentes, hashes, timeouts, rollback
    BackupSetId Backup,                 // backup verificado obligatorio
    OwnershipRecord Ownership,          // verificación de propiedad
    ConsentRecord Consent,              // consentimiento firmado
    string Signature);                  // sin firma válida, no hay escritura
```

---

*Fin del documento. Las decisiones abiertas están en §18; el arranque inmediato está en §17.*
