# MobileFix Tool

Plataforma profesional de **diagnóstico, recuperación y reparación** de dispositivos móviles
(Android e iOS) para el mercado LATAM. Objetivo: ser la navaja suiza del taller, con evidencia
auditable y sin brickear equipos.

> Estado: **v0.1.0 — esqueleto navegable**. Sin hardware. Lo que ya existe es el pipeline de
> 13 etapas y las invariantes de seguridad que lo protegen.

---

## Qué hay implementado hoy

| Componente | Estado |
|---|---|
| Pipeline de 13 etapas como máquina de estados del dominio | ✅ |
| **Punto Único de Escritura**: solo la etapa 10 escribe, y solo con respaldo verificado + Safety Gate aprobado | ✅ |
| **Journal append-only encadenado por SHA-256** con verificación de integridad y detección de manipulación | ✅ |
| Orquestador de las 13 etapas con registro antes/después y fallo explícito | ✅ |
| Aplicación de escritorio (WPF) que recorre una sesión simulada | ✅ |
| **55 pruebas unitarias** del dominio, del journal y del motor de cobertura, con reloj fijo (deterministas) | ✅ |
| Autocomprobación ejecutable (CLI) de las invariantes | ✅ |
| **Motor de cobertura**: lee el inventario del banco, calcula la matriz y resuelve el veredicto por dispositivo | ✅ M2-03 |
| **Honestidad de cobertura**: procedencia declarada; con datos de ejemplo todo veredicto es PROVISIONAL | ✅ |
| **Catálogo del mercado mexicano**: 170 modelos con SoC, placa, firmware base, mecanismo y ruta de liberación | ✅ |
| **Bloqueo de operador**: 22 equipos importados de EEUU; liberación solo por el operador, nunca por bypass | ✅ §26 |
| Inventario de ejemplo (72 equipos) + plantilla para el banco real | ✅ |
| Identificación real (escalera L0–L6), hardware, plugins OEM | ⏳ M0–M2 |

**Nada de esto toca todavía un dispositivo real.** Es la base sobre la que se construye el HAL.

---

## Requisitos

- **Windows 10 o superior**
- **.NET 10 SDK** (LTS, soporte hasta noviembre de 2028)
  - Instalación sin permisos de administrador, en la carpeta del usuario:
    ```powershell
    $dir = "$env:LOCALAPPDATA\dotnet-sdk"
    Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -OutFile "$dir\dotnet-install.ps1"
    & "$dir\dotnet-install.ps1" -Channel 10.0 -Quality GA -InstallDir $dir -NoPath
    ```
- **Visual Studio 2026** *(recomendado para desarrollo)*. Visual Studio 2022 **no puede** apuntar a .NET 10.
  Alternativa: VS Code + C# Dev Kit.

> ⚠️ No arrancar sobre .NET 8 ni .NET 9: ambas llegan a fin de soporte el **10 de noviembre de 2026** (ADR-007).

---

## Compilar y ejecutar

```powershell
.\build.ps1              # compila la solución
.\build.ps1 -Test        # pruebas unitarias + autocomprobación de invariantes
.\build.ps1 -Publish     # genera artifacts\desktop-standalone\MobileFix.exe (autocontenido)
.\build.ps1 -Run         # arranca la aplicación de escritorio
.\build.ps1 -Release     # compila en Release
```

El ejecutable autocontenido **no necesita .NET instalado en la máquina de destino**: se puede copiar
a la PC de un taller y ejecutar. Es la forma de enseñar el avance sin pedir a nadie que instale nada.

O directamente:

```powershell
dotnet build MobileFix.slnx
dotnet run --project src/MobileFix.Cli
dotnet run --project src/MobileFix.Desktop

# Matriz de cobertura del banco desde el inventario
dotnet run --project src/MobileFix.Cli -- --coverage docs/inventario-demo.csv
```

### Catálogo e inventario

Son dos cosas distintas y se mantienen separadas a propósito:

| Archivo | Qué es |
|---|---|
| `docs/catalogo-mx.csv` | **Conocimiento**: 170 modelos del mercado mexicano con las variantes de operador de EEUU. SoC, placa, firmware base, mecanismo de acceso, ruta de liberación y nivel de confianza del dato |
| `docs/CATALOGO-MX.xlsx` | La misma información para verla en Excel |
| `docs/inventario-demo.csv` | **Evidencia**: 72 equipos del banco de ejemplo (22 importados de EEUU y bloqueados a operador). Es lo que lee el motor de cobertura |
| `docs/INVENTARIO-BANCO-DEMO.xlsx` | La misma información para verla en Excel |
| `docs/INVENTARIO-BANCO.xlsx` | **Plantilla vacía** para fichar el banco real (M0-01) |

> **El catálogo describe qué ES cada equipo. El inventario describe qué se ha MEDIDO con él.**
> Mezclarlos hace que la matriz de cobertura mienta, así que el motor de cobertura solo lee el inventario,
> y con `provenance=seeded-demo` todo veredicto sale marcado **PROVISIONAL**. Solo con `provenance=bench`
> la plataforma declara cobertura medida.

Los campos del catálogo con `confidence=media` deben validarse contra el equipo físico antes de darlos por
buenos: un código de modelo o un SoC equivocado contamina la identificación.

La autocomprobación crea su journal en `%LOCALAPPDATA%\MobileFixDemo\` y termina con código 0
solo si **las cinco invariantes** se cumplen:

1. Las 13 etapas se ejecutan en orden y quedan registradas.
2. Sin respaldo verificado no hay escritura.
3. El Safety Gate bloquea la escritura cuando falta un requisito.
4. El orden del pipeline no se puede saltar.
5. El journal detecta cualquier alteración de sus entradas.

---

## Estructura

```
src/
  MobileFix.Domain/          Modelo, reglas, journal (hash chain). CERO dependencias de I/O.
  MobileFix.Ports/           Contratos hacia el exterior (IJournal, IClock, ...).
  MobileFix.Application/     Orquestación de las 13 etapas y Safety Gate.
  MobileFix.Infrastructure/  Implementaciones: journal en disco, reloj.
  MobileFix.Cli/             Autocomprobación de invariantes (ejecutable).
  MobileFix.Desktop/         Aplicación WPF.
docs/                        Plan maestro, backlog, design tokens, inventario del banco.
```

**Regla de dependencias:** `Domain` no referencia nada. `Application` no referencia `Infrastructure`.
Si esa flecha aparece, el diseño se ha roto.

---

## Decisiones de arquitectura vigentes

| ADR | Decisión |
|---|---|
| ADR-001 | **WPF sobre .NET 10 LTS** (no WinUI 3, no Avalonia, no Tauri) |
| ADR-002 | **DevExpress WPF** como suite de controles e informes (XtraReports) |
| ADR-003 | **Un solo motor de tema**, generado desde `docs/DESIGN-TOKENS.md` |
| ADR-004 | **SQLite + Dapper**, sin ORM. El journal va en archivo append-only |
| ADR-005 | **MSI (WiX v5) + firma Authenticode EV + auto-update firmado** |
| ADR-006 | **.NET 10 LTS + Visual Studio 2026** |
| ADR-007 | Prohibido arrancar sobre .NET 8 o .NET 9 (EOL 10-nov-2026) |

Detalle completo en [`docs/PLAN-MAESTRO.md`](docs/PLAN-MAESTRO.md) §25.

---

## Alcance prohibido (permanente)

No se implementa, ni en modo de depuración: bypass de FRP / Activation Lock / iCloud Lock,
reescritura o generación de IMEI, desbloqueo de equipos con propiedad no verificada, uso de
loaders o auth files de origen filtrado, explotación de vulnerabilidades del bootrom para
eludir bloqueos de activación, ni desbloqueo de operador mediante servicios no autorizados.

**Principio rector:** la plataforma restaura la identidad propia del equipo; nunca crea una
identidad nueva.

---

## Documentación

- [`docs/PLAN-MAESTRO.md`](docs/PLAN-MAESTRO.md) — arquitectura, pipeline, roadmap, ADR
- [`docs/BACKLOG-M0-M2.md`](docs/BACKLOG-M0-M2.md) — tareas asignables de los tres primeros hitos
- [`docs/DESIGN-TOKENS.md`](docs/DESIGN-TOKENS.md) — sistema de diseño y semántica de riesgo
- [`docs/INVENTARIO-BANCO.xlsx`](docs/INVENTARIO-BANCO.xlsx) — plantilla del banco de pruebas (M0-01)
