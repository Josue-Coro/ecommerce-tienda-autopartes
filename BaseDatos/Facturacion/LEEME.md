# Módulo de Facturación (modo simulado)

Facturación **Computarizada en Línea**, documento sector **1 – Factura Compra Venta**, siguiendo el
Anexo Técnico del SIN (RND 102100000011). Mientras no exista un NIT autorizado, trabaja contra un
**SIN simulado** que aplica las mismas reglas del anexo, pero sus facturas **no tienen validez tributaria**.

## Instalación

1. Ejecutar `01_Esquema_Facturacion.sql` sobre la base `DOSHERMANOS` (SQL Server 2016 SP1 o superior).
   Crea el esquema `FACTURACION`; no modifica ninguna tabla existente y puede ejecutarse varias veces.
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

## Pendiente (siguientes etapas)

- Emisión **fuera de línea**, eventos significativos y envío de paquetes (contingencia).
- Conexión real a los servicios SOAP del SIN: implementar `ISiatServicio` y usarlo en `SiatServicioFactory`.
- Notas de crédito-débito.

Los catálogos de productos/servicios, actividades, unidades y leyendas del simulador son **datos de ejemplo**;
al conectar con el SIN real se reemplazan por los oficiales y hay que volver a homologar.
