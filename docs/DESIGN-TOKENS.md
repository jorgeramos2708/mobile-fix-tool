# DESIGN TOKENS — Sistema de diseño de la plataforma

> Fuente única de verdad para color, tipografía, espaciado, estados y **semántica de dominio**.
> De aquí se generan: el tema de DevExpress (código), las variables de Figma y la documentación.
> Versión 0.1 (borrador para validar en M0). Ver §24 del PLAN-MAESTRO.

---

## 0. Reglas del sistema

1. **Un solo origen.** Ningún color, tamaño o espaciado se escribe a mano en XAML ni en Figma. Todo sale de aquí.
2. **Tres capas:** `primitivo → semántico → componente`. Solo los semánticos se usan en la UI.
3. **Nunca solo color.** Todo estado lleva color + icono + texto o patrón (accesibilidad y daltonismo).
4. **El rojo no se negocia.** `risk.brick` es el único token rojo saturado del sistema. Nada más lo usa.
5. **Nombres en inglés, documentación en español.** Los identificadores no se traducen; las etiquetas sí (i18n ES/PT).

---

## 1. Primitivos (paleta base)

| Token | Valor | Uso |
|---|---|---|
| `color.neutral.0` | `#FFFFFF` | Superficie base (tema claro) |
| `color.neutral.50` | `#F7F8FA` | Superficie alterna |
| `color.neutral.200` | `#D8DCE3` | Bordes suaves |
| `color.neutral.500` | `#6B7280` | Texto secundario |
| `color.neutral.800` | `#1F2430` | Superficie base (tema oscuro) |
| `color.neutral.900` | `#141821` | Fondo (tema oscuro) |
| `color.neutral.950` | `#0C0F15` | Fondo de consola |
| `color.blue.500` | `#2563EB` | Acción primaria |
| `color.amber.500` | `#D97706` | Precaución |
| `color.red.600` | `#DC2626` | **Reservado: riesgo de brick** |
| `color.green.600` | `#059669` | Éxito / verificado |
| `color.teal.600` | `#0D9488` | Enlace saludable |

**Regla:** un primitivo nunca se usa directamente en la UI. Siempre a través de un semántico.

---

## 2. Semánticos de interfaz

| Token | Claro | Oscuro | Uso |
|---|---|---|---|
| `bg.canvas` | `neutral.50` | `neutral.950` | Fondo de la app |
| `bg.surface` | `neutral.0` | `neutral.900` | Paneles y tarjetas |
| `bg.surface.raised` | `neutral.0` + sombra 1 | `neutral.800` | Diálogos, popovers |
| `border.subtle` | `neutral.200` | `#2A3140` | Separadores |
| `text.primary` | `#111827` | `#E8EAED` | Texto principal |
| `text.secondary` | `neutral.500` | `#9AA3B2` | Metadatos, unidades |
| `accent.primary` | `blue.500` | `#60A5FA` | Acción principal de la etapa |
| `focus.ring` | `blue.500` @ 2px offset 2 | idem | Foco de teclado (siempre visible) |

---

## 3. Semánticos de dominio (el corazón del sistema)

### 3.1 Riesgo de operación — `risk.*`

| Token | Color | Forma obligatoria | Significado |
|---|---|---|---|
| `risk.safe` | `green.600` | Icono escudo relleno | Solo lectura, reversible, sin pérdida |
| `risk.caution` | `amber.500` | Icono triángulo | Escribe, pero con backup verificado |
| `risk.destructive` | `amber.500` + borde grueso | Triángulo + texto “modifica datos” | Borra o altera datos del usuario |
| `risk.brick` | `red.600` | **Rojo saturado + rayado diagonal** | Puede dejar el equipo inutilizable |

`risk.brick` exige **doble confirmación** (ver §24.4-8) y aparece en: botones, cabecera de la etapa 10, y en el
informe. Es el único sitio del producto donde se permite el rojo saturado.

### 3.2 Calidad del enlace — `link.*` (barra de estado permanente)

| Token | Color | Etiqueta ES/PT | Criterio |
|---|---|---|---|
| `link.excellent` | `teal.600` | Excelente / Excelente | 0 errores, throughput nominal |
| `link.good` | `green.600` | Buena / Boa | Errores < umbral, sin reintentos |
| `link.unstable` | `amber.500` | Inestable / Instável | Reintentos o degradación → **advertencia antes de escribir** |
| `link.bad` | `red.600` | No apta para escribir / Não apta | **Bloquea la etapa 10** |
| `link.disconnected` | `neutral.500` | Desconectado | Sin dispositivo |

Regla de UX: la respuesta a *«¿puedo desconectar?»* debe ser legible **de un vistazo a 2 metros**. No es un
porcentaje: es una frase corta y un color.

### 3.3 Etapas del pipeline — `stage.*`

| Token | Estado visual | Significado |
|---|---|---|
| `stage.pending` | Contorno gris | No iniciada |
| `stage.active` | Relleno `accent.primary` + barra de progreso | En curso (con bytes/ETA si aplica) |
| `stage.done` | Check verde + hash de evidencia | Completada, con evidencia generada |
| `stage.failed` | Equis roja + causa raíz | Falló; no se marca como éxito jamás |
| `stage.blocked` | Candado ámbar + motivo textual | Bloqueada por un gate; debe decir **por qué** |
| `stage.skipped` | Gris punteado + razón | Omitida explícitamente, nunca en silencio |

### 3.4 Confianza del fingerprint — `confidence.*`

| Token | Rango | Presentación |
|---|---|---|
| `confidence.high` | ≥ 95 % | Verde + “identificado” |
| `confidence.medium` | 70–94 % | Ámbar + “probable (falta confirmar)” |
| `confidence.low` | < 70 % | Ámbar punteado + “identidad dudosa” |
| `confidence.conflict` | Discrepancia L5 vs L6 | **Ámbar + icono de alerta + advertencia de placa cambiada** |

### 3.5 Veredicto de cobertura — `coverage.*`

| Token | Veredicto | Color |
|---|---|---|
| `coverage.full` | Pleno | `green.600` |
| `coverage.partial` | Parcial | `blue.500` |
| `coverage.readonly` | Solo lectura | `neutral.500` + icono ojo |
| `coverage.blocked` | Bloqueado por autorización | `amber.500` + icono candado |
| `coverage.unsupported` | No soportado | `red.600` atenuado + explicación técnica |

### 3.6 Procedencia de firmware — `provenance.*`

| Token | Nivel | Color |
|---|---|---|
| `provenance.signed` | Oficial firmado | `green.600` |
| `provenance.official` | Oficial no firmado | `green.600` atenuado |
| `provenance.hashverified` | Verificado por hash | `teal.600` |
| `provenance.unknown` | Desconocido | `amber.500` |
| `provenance.tampered` | Alterado / sospechoso | `red.600` + bloqueo |

### 3.7 Evidencia — `evidence.*`

| Token | Uso |
|---|---|
| `evidence.captured` | Icono cámara + check: evidencia guardada, con hash visible |
| `evidence.pending` | Icono reloj: se generará al terminar la etapa |
| `evidence.missing` | Ámbar: etapa completada **sin** evidencia (alerta de auditoría) |
| `evidence.sealed` | Candado verde: paquete cerrado y firmado (informe/exportación) |

---

## 4. Tipografía

Fuente: **Segoe UI Variable** (Windows 11) con fallback **Segoe UI** (Windows 10). Consola: **Cascadia Mono**.

| Token | Tamaño / línea | Peso | Uso |
|---|---|---|---|
| `type.display` | 32 / 40 | 600 | Veredicto de triage, cifras grandes del dashboard |
| `type.title` | 20 / 28 | 600 | Título de etapa, cabecera de panel |
| `type.body` | 14 / 20 | 400 | Texto normal |
| `type.body.strong` | 14 / 20 | 600 | Etiquetas de campo |
| `type.caption` | 12 / 16 | 400 | Metadatos, unidades, hashes |
| `type.mono` | 12.5 / 18 | 400 | Log, hex, comandos, hashes |
| `type.number.large` | 28 / 32 | 600, tabular | Voltaje, corriente, porcentaje |

**Regla tabular:** todo número que cambia en vivo (amperaje, progreso, temperatura) usa **figuras tabulares**
para que no baile el layout. Es un detalle que separa una herramienta técnica de una app de consumo.

---

## 5. Espaciado, radios, elevación y densidad

- **Espaciado:** escala de 4 px → `space.1=4, .2=8, .3=12, .4=16, .6=24, .8=32, .12=48`.
- **Radios:** `radius.sm=4`, `radius.md=6`, `radius.lg=8`, `radius.pill=999`. Nada más redondeado que 8 px:
  las herramientas técnicas no son burbujas.
- **Elevación:** solo 2 niveles (`shadow.1` tarjetas, `shadow.2` diálogos). Sombras suaves, nunca dramáticas.
- **Densidad:** tres modos conmutables en caliente:

| Modo | Altura de fila | Tipografía | Cuándo |
|---|---|---|---|
| `density.compact` | 24 px | `caption` | Tabla de particiones, log, listas largas |
| `density.default` | 32 px | `body` | Uso normal |
| `density.bench` | 44 px | `body` +2 pt | **Modo banco**: de pie, a distancia, con guantes |

---

## 6. Reglas de formulario y confirmación

| Nivel | Componente | Interacción exigida |
|---|---|---|
| Reversible | Botón normal | Un clic |
| Escribe con backup | Diálogo con resumen del plan | Un clic + checkbox “tengo backup verificado” |
| Destructivo | Diálogo con lista de lo que se pierde | **Mantener pulsado 2 s** |
| `risk.brick` | Diálogo bloqueante + resumen de riesgo | **Escribir el modelo del equipo** + firma de supervisor si el riesgo > umbral |

---

## 7. i18n y accesibilidad

- **ES y PT desde el día 1.** Toda cadena en recursos. Tolerancia de **+25 % de longitud** en botones y etiquetas
  (el portugués es más largo que el español; el inglés, más corto).
- Contraste mínimo **AA (4.5:1)** en texto; **3:1** en elementos de interfaz.
- **Ningún estado depende solo del color** (ver §0.3).
- Foco de teclado **siempre visible**, orden de tabulación lógico, atajos para las 13 etapas.
- Compatibilidad con Narrator: `AutomationProperties.Name` en todo control interactivo. Es requisito, no extra:
  en varios países de LATAM la accesibilidad es exigible para vender a empresas.

---

## 8. Mapeo a implementación

**XAML (tema DevExpress generado):**

```xml
<!-- Generado desde design/tokens. NO editar a mano. -->
<Color x:Key="RiskBrickColor">#DC2626</Color>
<SolidColorBrush x:Key="RiskBrickBrush" Color="{StaticResource RiskBrickColor}" />
<sys:Double x:Key="DensityBenchRowHeight">44</sys:Double>
<FontFamily x:Key="TypeMonoFamily">Cascadia Mono, Consolas, monospace</FontFamily>
```

**Figma:** se importan como *variables* con la misma jerarquía (`risk/brick`, `stage/active`, `link/unstable`).
Los nombres deben coincidir **carácter a carácter** con los tokens de código: si divergen, el handoff se rompe.

**Validación automática (en CI):**
1. Todo token usado en XAML existe en `design/tokens`.
2. Ningún color literal (`#RRGGBB`) fuera de `design/tokens`.
3. Contraste de cada par texto/fondo calculado y verificado ≥ AA.
4. Los tokens `risk.*`, `stage.*` y `link.*` están presentes en ambos temas (claro y oscuro).

---

## 9. Lo que este sistema prohíbe

- Rojo saturado fuera de `risk.brick`.
- Colores literales en XAML o en Figma.
- Iconos o animaciones decorativas en pantallas de operación: el técnico está trabajando, no navegando.
- Modales apilados (regla dura: nunca dos diálogos simultáneos; el flujo se rediseña).
- Progreso indeterminado en una operación de escritura: si no se sabe el avance, se muestra el paso actual y el
  tiempo transcurrido, nunca una barra girando sin fin.
