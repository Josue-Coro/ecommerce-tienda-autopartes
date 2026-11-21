# Módulo de Contabilidad

Contabilidad integrada con Ventas, Compras y Facturación: los asientos de las facturas, notas de
crédito-débito, anulaciones y compras se generan automáticamente.

## Instalación

1. Tener instalado el módulo de Facturación (`BaseDatos/Facturacion/01_Esquema_Facturacion.sql`).
2. Ejecutar `01_Esquema_Contabilidad.sql` sobre `DOSHERMANOS`. Crea el esquema `CONTABILIDAD`, carga un plan de
   cuentas inicial y no modifica tablas existentes. Puede ejecutarse varias veces.
3. Menú **Contabilidad**: visible para los roles ADMINISTRADOR, GERENTE y CONTADOR (el rol CONTADOR se crea en
   Administración → Roles si se necesita). Cerrar y reabrir meses: solo ADMINISTRADOR y GERENTE.

## Uso

| Pantalla | Qué hace |
|---|---|
| Plan de cuentas | Cuentas jerárquicas (1 Activo … 5 Egresos), alta de subcuentas y elección de las cuentas que usan los asientos automáticos. |
| Libro diario | **Contabilizar documentos** del rango (idempotente: no duplica), asientos manuales (debe = haber), anulación de asientos manuales y cierre de meses. |
| Libro mayor | Movimientos y saldo de una cuenta, con saldo anterior. |
| Estados financieros | Balance de comprobación de sumas y saldos, Estado de resultados y Balance general. |
| Libros de compras y ventas | Libros con la estructura del RCV del SIN, descarga en CSV y registro de la factura del proveedor en cada compra. |
| Liquidación IVA / IT | IVA del mes (débito − crédito − saldo a favor anterior) e IT 3 %, con saldo a favor arrastrado. |

## Asientos automáticos (precios con IVA incluido)

| Documento | Debe | Haber |
|---|---|---|
| Factura | Caja / Bancos / Ctas. por cobrar (según método de pago); IT (gasto) 3 %; Costo de ventas | Ventas y Servicios (87 %); Débito fiscal IVA (13 %); IT por pagar; Inventario |
| Nota crédito-débito | Devoluciones sobre ventas (87 %); Crédito fiscal (13 %); IT por pagar; Inventario si reingresa stock | Caja / Bancos; IT (gasto); Costo de ventas |
| Compra | Inventario; Crédito fiscal 13 % (solo con factura del proveedor, tipo 1) | Caja / Bancos / Ctas. por pagar |
| Anulación | Asiento inverso al del documento original | |

- El **costo de ventas** usa el último precio de compra del producto (sin IVA si esa compra tiene factura registrada).
  Si el producto no tiene compras registradas, la venta se contabiliza sin costo.
- Si se registra la factura del proveedor de una compra ya contabilizada, su asiento se anula y se regenera al volver
  a contabilizar.
- Un mes cerrado no admite asientos, anulaciones ni cambios en las facturas de compra.

## Simplificaciones (a revisar con un contador)

- No se generan asientos de liquidación mensual de IVA ni de pago de impuestos, ni asientos de cierre de gestión.
- El saldo a favor de IVA no se actualiza por UFV; no se compensa el IT con el IUE.
- Las facturas anuladas figuran en el Libro de Ventas con importes en cero y estado "A".
- Costo de ventas al último precio de compra (no promedio ponderado).
