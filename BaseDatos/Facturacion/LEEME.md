# Módulo de Facturación (modo simulado)

Facturación **Computarizada en Línea**, documento sector **1 – Factura Compra Venta**, siguiendo el
Anexo Técnico del SIN (RND 102100000011). Mientras no exista un NIT autorizado, trabaja contra un
**SIN simulado** que aplica las mismas reglas del anexo, pero sus facturas **no tienen validez tributaria**.

## Instalación

1. Ejecutar `01_Esquema_Facturacion.sql` sobre la base `DOSHERMANOS` (SQL Server 2016 SP1 o superior).
   Crea el esquema `FACTURACION`; no modifica ninguna tabla existente y puede ejecutarse varias veces.
   Si ya lo había ejecutado antes, vuelva a ejecutarlo: agrega lo nuevo (contingencia, notas) sin perder datos.
2. Compilar la solución en Visual Studio y entrar con un usuario ADMINISTRADOR o GERENTE.

## Puesta en marcha (menú Facturación)

1. **Configuración → Datos del emisor**: NIT, razón social, actividad económica (vienen datos de ejemplo).
2. **Configuración → Sucursales**: registrar cada tienda (0 = Casa Matriz), pedir **CUIS** y luego **CUFD**.
3. **Configuración → Catálogos**: *Sincronizar ahora*.
4. **Homologación SIN**: asignar código del SIN a productos, servicios, unidades y tipos de pago.
5. Registrar una venta: al terminar, el sistema ofrece **Emitir factura**. También desde **Facturas → Ventas por facturar**.

## Qué implementa

| Anexo técnico | Implementación |
|---|---|
| CUIS (365 días) / CUFD (24 h, renovación automática) | `CN_ConfiguracionFacturacion` |
| CUF: cadena de 53 dígitos + Módulo 11 → Base 16 + código de control | `AlgoritmosSiat.GenerarCuf` (verificado con el ejemplo oficial) |
| XML Compra Venta validado con el XSD oficial | `GeneradorXmlFactura` + `Esquemas/facturaComputarizadaCompraVenta.xsd` |
| GZip + SHA-256 del archivo, recepción 908/904 | `CN_Factura.Emitir` + `SiatServicioSimulado` |
| Verificación de NIT y código de excepción, códigos 99001/99002/99003 | `CN_Factura` |
| Tarjeta enmascarada, redondeo HALF-UP, leyenda Ley 453 aleatoria | `AlgoritmosSiat`, `CN_Factura` |
| Anulación con motivo hasta el día 9 del mes siguiente + aviso por correo | `CN_Factura.Anular` |
| Representación gráfica (carta / rollo 80 mm) con QR de verificación | `Views/Facturacion/Representacion.cshtml` |
| Contingencia: paso automático a fuera de línea si el SIN no responde, o manual (eventos 1–4) | `CN_Contingencia`, `CN_Factura.Emitir` |
| Emisión fuera de línea: tipo de emisión 2, CUFD del evento (hasta 72 h), NIT con excepción 1, leyenda "fuera de línea" | `CN_Factura` |
| Cierre: nuevo CUFD → registro del evento → paquetes `.tar.gz` de hasta 500 → validación por factura (plazo 48 h) | `CN_Contingencia.FinalizarYEnviar`, `ArchivoTar` |
| Nota Crédito-Débito (sector 24, documento de ajuste 3): detalle original (1) + devuelto (2), crédito/débito fiscal 13 %, XSD oficial | `CN_NotaCreditoDebito`, `GeneradorXmlFactura.GenerarNota` |
| Anulación de notas y bloqueo de anulación de facturas con notas vigentes | `CN_NotaCreditoDebito.Anular`, `CN_Factura.Anular` |

## Contingencia (menú Facturación → Contingencia)

- Muestra cada tienda **en línea** o **fuera de línea** y el historial de eventos significativos.
- En modo simulado, el interruptor **"SIN sin conexión"** hace que el SIN simulado deje de responder:
  la siguiente factura pasa sola a fuera de línea. Al desactivarlo, **Finalizar y enviar** registra el evento
  y envía el paquete; cada factura queda VALIDADA u OBSERVADA según el resultado.
- Una factura OBSERVADA no cuenta como emitida: su venta vuelve a "Ventas por facturar".

## Notas crédito-débito (devoluciones)

- Se emiten desde **Facturas**, con el botón de devolución de una factura VALIDADA: se indica cuánto se devuelve de
  cada ítem y el motivo. No se puede devolver más de lo facturado, contando las notas anteriores.
- Opcionalmente reingresan al inventario los productos devueltos; si la nota se anula, el reingreso se revierte.
- Solo se emiten **en línea** (el servicio del SIN no las recibe fuera de línea).
- El listado, la impresión, el XML y la anulación están en **Facturación → Notas crédito-débito**.

## Pendiente (siguientes etapas)

- Eventos 5, 6 y 7 (falla de software/hardware, corte de energía): facturas manuales de contingencia (CAFC) y su transcripción.
- Emisión masiva (no aplica al giro de una tienda de autopartes).
- Conexión real a los servicios SOAP del SIN: implementar `ISiatServicio` y usarlo en `SiatServicioFactory`.

Los catálogos de productos/servicios, actividades, unidades y leyendas del simulador son **datos de ejemplo**;
al conectar con el SIN real se reemplazan por los oficiales y hay que volver a homologar.
