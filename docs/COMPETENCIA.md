# ANÁLISIS COMPETITIVO Y DIFERENCIAL

> Qué vende el mercado de herramientas de reparación, qué no vende nadie, y qué hay que implementar
> para que esta plataforma sea **más confiable y más robusta** que todas ellas.
> Complemento del PLAN-MAESTRO §23 y §26. Datos de mercado verificados en septiembre de 2026.

---

## 1. El mercado hoy

| Herramienta | Modelo de negocio | Fuerte en | Debilidad estructural |
|---|---|---|---|
| **Chimera Tool** | Licencia anual (Basic / Professional ≈ 149 USD / Premium), créditos para procedimientos "de pago", autenticador USB, 24 cambios de PC al año | +13.000 modelos, +30 fabricantes, +20 modos. Actualización cada 2 semanas. Base de datos viva de modelo + procedimiento. Librería de puntos de prueba | Vende FRP e IMEI (terreno que nosotros tenemos prohibido). Sin evidencia. Sin rollback. Sin certificado |
| **UnlockTool** | Activación por tiempo (3, 6, 12 meses) vía distribuidores. **Se alquila por horas** en el mercado gris | MTK/Qualcomm/Samsung/Xiaomi, releases frecuentes, cuenta portable | Sin verificación previa de nada. El alquiler por horas fomenta el "probar a ver qué pasa" |
| **UMT (Ultimate Multi Tool)** | Ecosistema de módulos y hardware | Cobertura por módulo, comunidad | Dependencia de hardware propietario, curva de entrada alta |
| **3uTools / iMazing** | Gratis / licencia perpetua | Ecosistema Apple, muy pulido | Solo Apple. No es una plataforma de taller |
| **Odin / Heimdall** | Gratis / open source | Flasheo Samsung | Protocolo único, sin verificación, interfaz de otro siglo |
| **Cajas (Easy JTAG, Miracle…)** | Hardware + licencias | Reparación a nivel físico | Otra categoría: no compite en software de diagnóstico |

**Dato clave del sector**, extraído de una comparativa técnica de 2026: *«no bases una compra en una
captura de pantalla de una versión antigua cuando tu teléfono objetivo tiene un BIT, un parche de
seguridad o un requisito de loader más nuevos»*. Esa frase describe el problema real del mercado:
**las herramientas prometen capacidades por modelo, pero fallan por firmware concreto**. Nadie resuelve
esa brecha.

---

## 2. Lo que todos venden, y lo que no vende nadie

Todos venden **capacidades**: lista de modelos, cantidad de procedimientos, velocidad de actualización.

Nadie vende **garantías**:

| Pregunta del taller | ¿La responde la competencia? | ¿La responde esta plataforma? |
|---|---|---|
| ¿Esto va a funcionar con **este** equipo y **este** build? | No. "Soportado" por modelo, no por firmware | Sí: matriz de cobertura medida por el propio taller |
| ¿Qué pasa si se corta la luz a mitad? | No lo dice. Se descubre en el peor momento | Reanudable + banco de inyección de fallos |
| ¿Puedo deshacerlo? | No hay rollback estructural | Punto de retorno verificado antes de escribir |
| ¿Cómo demuestro lo que hice? | No hay registro. Capturas de pantalla | Journal encadenado por hash + certificado firmado |
| ¿Cómo demuestro que **no** toqué los datos del cliente? | Ni lo plantean | Sesión auditada con prueba de no-acceso |
| ¿Este firmware es auténtico? | No lo comprueba | Procedencia y autenticidad verificadas |
| ¿Mi cable está bien? | No lo mide: escribe y reza | Verificación de calidad de enlace antes de escribir |

---

## 3. Los cuatro fallos que más le cuestan dinero al taller

1. **Flasheo con firmware de otra variante (CSC/región equivocada).** Es la causa número uno de bricks
   evitables. Ninguna herramienta lo bloquea de verdad.
2. **Escritura con enlace inestable.** Cable o hub malos. La herramienta escribe igual y se cae a mitad.
3. **Operación aplicada a un firmware más nuevo que el que la herramienta validó** (BIT, parche, loader).
4. **Cero rastro.** Cuando algo sale mal, no hay forma de saber qué pasó ni de defenderlo ante el cliente.

Los cuatro se resuelven con **gates**, no con más procedimientos. Y aquí está la asimetría
competitiva: a la competencia no le interesa poner gates, porque su marketing es "hace más cosas".

---

## 4. Diferenciales que ya están en el plan

- Pipeline de 13 etapas con **Punto Único de Escritura** (§2)
- **Journal encadenado por hash** con detección de manipulación (§2, §21.4)
- **Matriz de cobertura honesta**, con procedencia del dato declarada (§5)
- **Punto de riesgo de brick** por operación, con aprobación de supervisor (§20.3)
- **Procedencia y autenticidad de firmware** con bloqueo por variante regional (§20.4)
- **Sesión auditada y prueba de no-acceso** a los datos del cliente (§21.3)
- **Certificado de reparación firmado** con IMEI intacto verificado (§21.4)
- **Verificación de calidad de enlace** antes de escribir (§21.10)
- **Reanudación tras corte** probada con inyección de fallos (§21.10)
- **Liberación de operador solo por vía legítima**, documentada (§26)

---

## 5. Diferenciales nuevos: lo que hay que implementar para no parecerse a nadie

Ordenados por relación impacto/esfuerzo. Ninguno existe en la competencia.

| # | Diferencial | Qué resuelve | Esfuerzo | Defendible |
|---|---|---|---|---|
| 1 | **Gate por parche de seguridad y versión de loader** | El fallo nº 3 del sector. Se lee el parche (ya lo hacemos, nivel L4) y se **rechaza** la operación fuera de la ventana validada, en vez de avisar | Bajo | Alto |
| 2 | **Ensayo previo verificado (dry-run)** | Calcula el plan completo contra la GPT real del equipo —offsets, tamaños, hashes, orden— y verifica que todo encaja **sin escribir un byte**. Si algo no cuadra, se sabe antes | Medio | Muy alto |
| 3 | **Punto de retorno verificado** | Respaldo de las regiones exactas que se van a tocar, con verificación de integridad de la copia, y botón de deshacer. Nadie ofrece "deshacer un flasheo" | Medio | Muy alto |
| 4 | **Acta de estado de entrada firmada** | Foto firmada del estado del equipo al recibirlo (identidad, GPT, hashes de particiones críticas). El taller puede demostrar cómo le llegó el equipo | Bajo | Alto |
| 5 | **Límite de daño visible** | Antes de confirmar: qué particiones se tocan, cuáles se pierden, si `userdata` se conserva. El técnico decide con información, no con fe | Bajo | Medio-alto |
| 6 | **Verificación funcional posterior** | No basta con que arranque: Play Integrity, Widevine, banda base, sensores, cámaras. Un equipo que enciende pero falla Play Integrity es una reparación fallida para el cliente | Medio | Alto |
| 7 | **Historial del equipo por hash de IMEI/serial** | Detecta reincidencias y equipos con historial oculto ("este equipo ya estuvo aquí y le hicieron X") | Bajo | Medio |
| 8 | **Modo testigo** | Sesión con el cliente presente y acta inicial/final firmada por ambos. Para equipos de alto valor y peritajes | Bajo | Medio |
| 9 | **Criterios de aborto escritos en el plan** | Reintentos acotados, timeouts y códigos de error que obligan a parar. La regla es: nunca reintentar a ciegas | Bajo | Alto |
| 10 | **Certificado de cobertura verificada** | "Este taller puede hacer T2 en N modelos, medido". Vendible a cadenas, aseguradoras y peritajes | Medio | Muy alto |
| 11 | **Soporte con bundle de un clic + modo ingeniero remoto** | La competencia da soporte por Telegram en inglés. Aquí: envío del journal redactado y asistencia en vivo | Medio | Alto |
| 12 | **Interoperabilidad con el POS del taller** | No obligar a cambiar de sistema: API local y exportación | Bajo | Medio |

**Los tres que yo implementaría primero** son el **1**, el **2** y el **3**: son los que atacan
directamente los fallos que le cuestan dinero al taller, y los tres son **imposibles de copiar rápido**
porque exigen una arquitectura que la competencia no tiene (identidad medida, plan auditable y
journal encadenado).

---

## 6. Los seis principios de confiabilidad

1. **Nunca escribir sin haber leído.** La GPT se lee y se valida antes de proponer cualquier escritura.
2. **Verificar por bloque, no al final.** Escritura con verificación inmediata de cada bloque, no "flashear y rezar".
3. **El enlace manda.** Enlace inestable ⇒ no se escribe, sin discusión y sin opción de forzar.
4. **Nunca reintentar a ciegas.** Cada reintento tiene criterio y límite; si se agota, se para y se explica por qué.
5. **Todo queda en el journal, con fsync.** Una operación que no está en el journal no ocurrió.
6. **Lo que no se ha probado contra el banco, no se afirma.** Y lo que se probó con cortes inyectados, se puede demostrar.

---

## 7. Qué NO copiar

- **FRP e IMEI.** Es el 40 % del negocio de la competencia y el 100 % de su riesgo legal. Nuestro §26 lo prohíbe y hay una prueba unitaria que lo sostiene.
- **Marketing de "13.000 modelos soportados".** Nosotros publicamos cobertura **medida**, aunque el número sea menor. Un número honesto y creíble vende más a un taller con seguro.
- **Alquiler por horas.** Fomenta el "probar a ver qué pasa", que es exactamente la cultura que produce bricks.
- **Soporte solo en inglés y por Telegram.** En LATAM el soporte en español es una función del producto.
- **Interfaz de infierno de modales.** Odin y UnlockTool son el manual de lo que no hay que hacer (§24.5).

---

## 8. Conclusión

La competencia compite en **cantidad de capacidades**. Nosotros competimos en **certeza**:
qué es este equipo, qué se ha demostrado con él, qué va a pasar si lo toco, cómo lo deshago y cómo lo
demuestro después.

Ese cambio de eje es el producto. Y no se puede improvisar: exige identidad medida (ya construida),
cobertura con procedencia (ya construida), plan auditable y journal encadenado (ya construido).
Por eso los tres primeros diferenciales de la tabla son alcanzables ahora y no en dos años.
