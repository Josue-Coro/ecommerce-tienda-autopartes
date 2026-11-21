/* =====================================================================================
   MÓDULO DE CONTABILIDAD - Esquema CONTABILIDAD
   - Plan de cuentas, asientos (manuales y automáticos), cierre de meses.
   - Datos fiscales de las compras (para el Libro de Compras y el crédito fiscal).
   - Requiere el esquema FACTURACION (BaseDatos/Facturacion/01_Esquema_Facturacion.sql).
   - No modifica tablas existentes. Es idempotente. SQL Server 2016 SP1 o superior.
   ===================================================================================== */

USE [DOSHERMANOS]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'CONTABILIDAD')
    EXEC('CREATE SCHEMA [CONTABILIDAD]');
GO

/* -------------------------------------------------------------------------------------
   1. PLAN DE CUENTAS
   ------------------------------------------------------------------------------------- */
IF OBJECT_ID('CONTABILIDAD.CUENTA', 'U') IS NULL
    CREATE TABLE CONTABILIDAD.CUENTA (
        id_cuenta    INT IDENTITY(1,1) NOT NULL CONSTRAINT CUENTA_PK PRIMARY KEY,
        codigo       VARCHAR(20)  NOT NULL CONSTRAINT CUENTA_UQ_CODIGO UNIQUE,
        nombre       VARCHAR(150) NOT NULL,
        nivel        TINYINT      NOT NULL,
        id_padre     INT          NULL CONSTRAINT CUENTA_PADRE_FK REFERENCES CONTABILIDAD.CUENTA (id_cuenta),
        tipo         VARCHAR(15)  NOT NULL,   -- ACTIVO, PASIVO, PATRIMONIO, INGRESO, EGRESO
        naturaleza   CHAR(1)      NOT NULL,   -- D = deudora, A = acreedora
        imputable    BIT          NOT NULL,   -- solo las cuentas imputables reciben movimientos
        estado       BIT          NOT NULL CONSTRAINT CUENTA_DF_ESTADO DEFAULT (1)
    );
GO

-- Plan de cuentas inicial (se inserta solo si la tabla está vacía).
IF NOT EXISTS (SELECT 1 FROM CONTABILIDAD.CUENTA)
BEGIN
    DECLARE @c TABLE (codigo VARCHAR(20), nombre VARCHAR(150), tipo VARCHAR(15), naturaleza CHAR(1));
    INSERT INTO @c VALUES
    ('1', 'ACTIVO', 'ACTIVO', 'D'),
    ('1.1', 'ACTIVO CORRIENTE', 'ACTIVO', 'D'),
    ('1.1.1', 'DISPONIBLE', 'ACTIVO', 'D'),
    ('1.1.1.01', 'Caja', 'ACTIVO', 'D'),
    ('1.1.1.02', 'Bancos', 'ACTIVO', 'D'),
    ('1.1.2', 'EXIGIBLE', 'ACTIVO', 'D'),
    ('1.1.2.01', 'Cuentas por cobrar clientes', 'ACTIVO', 'D'),
    ('1.1.2.02', 'Crédito fiscal IVA', 'ACTIVO', 'D'),
    ('1.1.3', 'REALIZABLE', 'ACTIVO', 'D'),
    ('1.1.3.01', 'Inventario de mercaderías', 'ACTIVO', 'D'),
    ('1.2', 'ACTIVO NO CORRIENTE', 'ACTIVO', 'D'),
    ('1.2.1', 'ACTIVO FIJO', 'ACTIVO', 'D'),
    ('1.2.1.01', 'Muebles y enseres', 'ACTIVO', 'D'),
    ('1.2.1.02', 'Equipos de computación', 'ACTIVO', 'D'),
    ('1.2.1.03', 'Herramientas', 'ACTIVO', 'D'),
    ('1.2.1.04', 'Depreciación acumulada', 'ACTIVO', 'A'),
    ('2', 'PASIVO', 'PASIVO', 'A'),
    ('2.1', 'PASIVO CORRIENTE', 'PASIVO', 'A'),
    ('2.1.1', 'OBLIGACIONES', 'PASIVO', 'A'),
    ('2.1.1.01', 'Cuentas por pagar proveedores', 'PASIVO', 'A'),
    ('2.1.1.02', 'Sueldos por pagar', 'PASIVO', 'A'),
    ('2.1.2', 'OBLIGACIONES FISCALES', 'PASIVO', 'A'),
    ('2.1.2.01', 'Débito fiscal IVA', 'PASIVO', 'A'),
    ('2.1.2.02', 'Impuesto a las transacciones por pagar', 'PASIVO', 'A'),
    ('2.1.2.03', 'IVA por pagar', 'PASIVO', 'A'),
    ('3', 'PATRIMONIO', 'PATRIMONIO', 'A'),
    ('3.1', 'CAPITAL Y RESULTADOS', 'PATRIMONIO', 'A'),
    ('3.1.1', 'CAPITAL', 'PATRIMONIO', 'A'),
    ('3.1.1.01', 'Capital social', 'PATRIMONIO', 'A'),
    ('3.1.2', 'RESULTADOS', 'PATRIMONIO', 'A'),
    ('3.1.2.01', 'Resultados acumulados', 'PATRIMONIO', 'A'),
    ('4', 'INGRESOS', 'INGRESO', 'A'),
    ('4.1', 'INGRESOS OPERATIVOS', 'INGRESO', 'A'),
    ('4.1.1', 'VENTAS', 'INGRESO', 'A'),
    ('4.1.1.01', 'Ventas de mercaderías', 'INGRESO', 'A'),
    ('4.1.1.02', 'Ingresos por servicios', 'INGRESO', 'A'),
    ('4.1.1.03', 'Devoluciones sobre ventas', 'INGRESO', 'D'),
    ('5', 'EGRESOS', 'EGRESO', 'D'),
    ('5.1', 'COSTOS', 'EGRESO', 'D'),
    ('5.1.1', 'COSTO DE VENTAS', 'EGRESO', 'D'),
    ('5.1.1.01', 'Costo de mercaderías vendidas', 'EGRESO', 'D'),
    ('5.2', 'GASTOS OPERATIVOS', 'EGRESO', 'D'),
    ('5.2.1', 'GASTOS DE ADMINISTRACIÓN Y VENTAS', 'EGRESO', 'D'),
    ('5.2.1.01', 'Impuesto a las transacciones', 'EGRESO', 'D'),
    ('5.2.1.02', 'Sueldos y salarios', 'EGRESO', 'D'),
    ('5.2.1.03', 'Alquileres', 'EGRESO', 'D'),
    ('5.2.1.04', 'Servicios básicos', 'EGRESO', 'D'),
    ('5.2.1.05', 'Gastos generales', 'EGRESO', 'D'),
    ('5.2.1.06', 'Depreciación', 'EGRESO', 'D');

    INSERT INTO CONTABILIDAD.CUENTA (codigo, nombre, nivel, tipo, naturaleza, imputable)
    SELECT codigo, nombre, LEN(codigo) - LEN(REPLACE(codigo, '.', '')) + 1, tipo, naturaleza,
           CASE WHEN LEN(codigo) - LEN(REPLACE(codigo, '.', '')) = 3 THEN 1 ELSE 0 END
    FROM @c;

    UPDATE H SET id_padre = P.id_cuenta
    FROM CONTABILIDAD.CUENTA H
    INNER JOIN CONTABILIDAD.CUENTA P ON P.codigo = LEFT(H.codigo, LEN(H.codigo) - CHARINDEX('.', REVERSE(H.codigo)))
    WHERE H.codigo LIKE '%.%';
END
GO

/* Cuentas que usan los asientos automáticos (configurables desde la aplicación). */
IF OBJECT_ID('CONTABILIDAD.CUENTA_CONFIG', 'U') IS NULL
BEGIN
    CREATE TABLE CONTABILIDAD.CUENTA_CONFIG (
        clave        VARCHAR(30)  NOT NULL CONSTRAINT CUENTA_CONFIG_PK PRIMARY KEY,
        descripcion  VARCHAR(150) NOT NULL,
        CUENTA_id    INT          NOT NULL CONSTRAINT CUENTA_CONFIG_CUENTA_FK REFERENCES CONTABILIDAD.CUENTA (id_cuenta)
    );

    INSERT INTO CONTABILIDAD.CUENTA_CONFIG (clave, descripcion, CUENTA_id)
    SELECT v.clave, v.descripcion, C.id_cuenta
    FROM (VALUES
        ('CAJA', 'Cobros y pagos en efectivo', '1.1.1.01'),
        ('BANCOS', 'Cobros y pagos con tarjeta, transferencia o depósito', '1.1.1.02'),
        ('CUENTAS_COBRAR', 'Ventas al crédito', '1.1.2.01'),
        ('CREDITO_FISCAL', 'IVA crédito fiscal (compras y devoluciones)', '1.1.2.02'),
        ('INVENTARIO', 'Inventario de mercaderías', '1.1.3.01'),
        ('CUENTAS_PAGAR', 'Compras al crédito', '2.1.1.01'),
        ('DEBITO_FISCAL', 'IVA débito fiscal (ventas)', '2.1.2.01'),
        ('IT_POR_PAGAR', 'Impuesto a las transacciones por pagar', '2.1.2.02'),
        ('VENTAS', 'Ventas de mercaderías', '4.1.1.01'),
        ('SERVICIOS', 'Ingresos por servicios', '4.1.1.02'),
        ('DEVOLUCIONES', 'Devoluciones sobre ventas', '4.1.1.03'),
        ('COSTO_VENTAS', 'Costo de mercaderías vendidas', '5.1.1.01'),
        ('IT_GASTO', 'Impuesto a las transacciones (gasto)', '5.2.1.01')
    ) v (clave, descripcion, codigo)
    INNER JOIN CONTABILIDAD.CUENTA C ON C.codigo = v.codigo;
END
GO

/* -------------------------------------------------------------------------------------
   2. ASIENTOS
   ------------------------------------------------------------------------------------- */
IF OBJECT_ID('CONTABILIDAD.ASIENTO', 'U') IS NULL
BEGIN
    CREATE TABLE CONTABILIDAD.ASIENTO (
        id_asiento      INT IDENTITY(1,1) NOT NULL CONSTRAINT ASIENTO_PK PRIMARY KEY,
        gestion         INT          NOT NULL,
        numero          INT          NOT NULL,
        fecha           DATE         NOT NULL,
        glosa           VARCHAR(500) NOT NULL,
        tipo            VARCHAR(15)  NOT NULL,   -- AUTOMATICO, MANUAL
        origen_tipo     VARCHAR(30)  NULL,       -- FACTURA, ANULACION_FACTURA, NOTA, ANULACION_NOTA, COMPRA
        origen_id       INT          NULL,
        estado          VARCHAR(10)  NOT NULL CONSTRAINT ASIENTO_DF_ESTADO DEFAULT ('VIGENTE'),   -- VIGENTE, ANULADO
        usuario         VARCHAR(100) NOT NULL,
        fecha_registro  DATETIME     NOT NULL CONSTRAINT ASIENTO_DF_REGISTRO DEFAULT (GETDATE()),
        CONSTRAINT ASIENTO_UQ_NUMERO UNIQUE (gestion, numero)
    );
    -- Un documento genera como máximo un asiento vigente.
    CREATE UNIQUE INDEX ASIENTO_UQ_ORIGEN ON CONTABILIDAD.ASIENTO (origen_tipo, origen_id)
        WHERE origen_tipo IS NOT NULL AND estado = 'VIGENTE';
END
GO

IF OBJECT_ID('CONTABILIDAD.ASIENTO_DETALLE', 'U') IS NULL
    CREATE TABLE CONTABILIDAD.ASIENTO_DETALLE (
        id_asiento_detalle  INT IDENTITY(1,1) NOT NULL CONSTRAINT ASIENTO_DETALLE_PK PRIMARY KEY,
        ASIENTO_id          INT           NOT NULL CONSTRAINT ASIENTO_DETALLE_ASIENTO_FK REFERENCES CONTABILIDAD.ASIENTO (id_asiento),
        CUENTA_id           INT           NOT NULL CONSTRAINT ASIENTO_DETALLE_CUENTA_FK REFERENCES CONTABILIDAD.CUENTA (id_cuenta),
        debe                DECIMAL(17,2) NOT NULL CONSTRAINT ASIENTO_DETALLE_DF_DEBE DEFAULT (0),
        haber               DECIMAL(17,2) NOT NULL CONSTRAINT ASIENTO_DETALLE_DF_HABER DEFAULT (0),
        glosa               VARCHAR(300)  NULL,
        CONSTRAINT ASIENTO_DETALLE_CK_MONTOS CHECK (debe >= 0 AND haber >= 0 AND (debe = 0 OR haber = 0) AND debe + haber > 0)
    );
GO

IF TYPE_ID('CONTABILIDAD.DetalleAsientoTipo') IS NULL
    CREATE TYPE CONTABILIDAD.DetalleAsientoTipo AS TABLE (
        CUENTA_id  INT           NOT NULL,
        debe       DECIMAL(17,2) NOT NULL,
        haber      DECIMAL(17,2) NOT NULL,
        glosa      VARCHAR(300)  NULL
    );
GO

IF OBJECT_ID('CONTABILIDAD.PERIODO_CERRADO', 'U') IS NULL
    CREATE TABLE CONTABILIDAD.PERIODO_CERRADO (
        gestion       INT          NOT NULL,
        mes           TINYINT      NOT NULL,
        usuario       VARCHAR(100) NOT NULL,
        fecha_cierre  DATETIME     NOT NULL CONSTRAINT PERIODO_DF_FECHA DEFAULT (GETDATE()),
        CONSTRAINT PERIODO_CERRADO_PK PRIMARY KEY (gestion, mes)
    );
GO

/* -------------------------------------------------------------------------------------
   3. DATOS FISCALES DE LAS COMPRAS (Libro de Compras)
   ------------------------------------------------------------------------------------- */
IF OBJECT_ID('CONTABILIDAD.COMPRA_FISCAL', 'U') IS NULL
    CREATE TABLE CONTABILIDAD.COMPRA_FISCAL (
        COMPRA_id_compra     INT           NOT NULL CONSTRAINT COMPRA_FISCAL_PK PRIMARY KEY
                                           CONSTRAINT COMPRA_FISCAL_COMPRA_FK REFERENCES TRANSACCIONES.COMPRA (id_compra),
        nit_proveedor        BIGINT        NOT NULL,
        razon_social         VARCHAR(240)  NOT NULL,
        codigo_autorizacion  VARCHAR(100)  NOT NULL,
        numero_factura       BIGINT        NOT NULL,
        fecha_factura        DATE          NOT NULL,
        importe_total        DECIMAL(17,2) NOT NULL,
        importe_no_sujeto    DECIMAL(17,2) NOT NULL CONSTRAINT COMPRA_FISCAL_DF_NS DEFAULT (0),   -- ICE, IEHD, tasas, exentos...
        descuentos           DECIMAL(17,2) NOT NULL CONSTRAINT COMPRA_FISCAL_DF_DESC DEFAULT (0),
        tipo_compra          TINYINT       NOT NULL CONSTRAINT COMPRA_FISCAL_DF_TIPO DEFAULT (1),  -- 1 = destino a actividades gravadas
        codigo_control       VARCHAR(17)   NULL
    );
GO

/* =====================================================================================
   PROCEDIMIENTOS
   ===================================================================================== */

-- ---------- Plan de cuentas ----------
CREATE OR ALTER PROCEDURE CONTABILIDAD.ListarCuentas
AS
BEGIN
    SET NOCOUNT ON;
    SELECT C.id_cuenta, C.codigo, C.nombre, C.nivel, C.id_padre, P.codigo AS codigo_padre, C.tipo, C.naturaleza,
           C.imputable, C.estado,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM CONTABILIDAD.ASIENTO_DETALLE D WHERE D.CUENTA_id = C.id_cuenta) THEN 1 ELSE 0 END AS BIT) AS tiene_movimientos
    FROM CONTABILIDAD.CUENTA C
    LEFT JOIN CONTABILIDAD.CUENTA P ON P.id_cuenta = C.id_padre
    ORDER BY C.codigo;
END
GO

CREATE OR ALTER PROCEDURE CONTABILIDAD.GuardarCuenta
    @IdCuenta    INT,
    @Codigo      VARCHAR(20),
    @Nombre      VARCHAR(150),
    @Imputable   BIT,
    @Estado      BIT,
    @Mensaje     VARCHAR(500) OUTPUT,
    @Resultado   BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Resultado = 0;

    DECLARE @CodigoPadre VARCHAR(20) = CASE WHEN CHARINDEX('.', @Codigo) > 0
                                            THEN LEFT(@Codigo, LEN(@Codigo) - CHARINDEX('.', REVERSE(@Codigo))) END;
    DECLARE @IdPadre INT, @Tipo VARCHAR(15), @Naturaleza CHAR(1), @PadreImputable BIT;
    SELECT @IdPadre = id_cuenta, @Tipo = tipo, @Naturaleza = naturaleza, @PadreImputable = imputable
    FROM CONTABILIDAD.CUENTA WHERE codigo = @CodigoPadre;

    IF @IdCuenta = 0
    BEGIN
        IF @CodigoPadre IS NULL OR @IdPadre IS NULL
        BEGIN
            SET @Mensaje = 'La cuenta padre ' + ISNULL(@CodigoPadre, '(ninguna)') + ' no existe. Los grupos principales (1 a 5) ya están creados.';
            RETURN;
        END
        IF @PadreImputable = 1
        BEGIN
            SET @Mensaje = 'La cuenta padre es imputable (recibe movimientos); no puede tener subcuentas.';
            RETURN;
        END
        IF EXISTS (SELECT 1 FROM CONTABILIDAD.CUENTA WHERE codigo = @Codigo)
        BEGIN
            SET @Mensaje = 'Ya existe una cuenta con el código ' + @Codigo + '.';
            RETURN;
        END
        INSERT INTO CONTABILIDAD.CUENTA (codigo, nombre, nivel, id_padre, tipo, naturaleza, imputable, estado)
        VALUES (@Codigo, @Nombre, LEN(@Codigo) - LEN(REPLACE(@Codigo, '.', '')) + 1, @IdPadre, @Tipo, @Naturaleza, @Imputable, @Estado);
        SET @Mensaje = 'Cuenta creada.';
    END
    ELSE
    BEGIN
        -- Solo se cambian nombre, estado e imputabilidad (si no tiene movimientos ni subcuentas).
        IF @Imputable = 0 AND EXISTS (SELECT 1 FROM CONTABILIDAD.ASIENTO_DETALLE WHERE CUENTA_id = @IdCuenta)
        BEGIN
            SET @Mensaje = 'La cuenta tiene movimientos: debe seguir siendo imputable.';
            RETURN;
        END
        IF @Imputable = 1 AND EXISTS (SELECT 1 FROM CONTABILIDAD.CUENTA WHERE id_padre = @IdCuenta)
        BEGIN
            SET @Mensaje = 'La cuenta tiene subcuentas: no puede ser imputable.';
            RETURN;
        END
        UPDATE CONTABILIDAD.CUENTA SET nombre = @Nombre, imputable = @Imputable, estado = @Estado WHERE id_cuenta = @IdCuenta;
        SET @Mensaje = 'Cuenta actualizada.';
    END
    SET @Resultado = 1;
END
GO

CREATE OR ALTER PROCEDURE CONTABILIDAD.ListarCuentaConfig
AS
BEGIN
    SET NOCOUNT ON;
    SELECT G.clave, G.descripcion, G.CUENTA_id AS id_cuenta, C.codigo, C.nombre
    FROM CONTABILIDAD.CUENTA_CONFIG G
    INNER JOIN CONTABILIDAD.CUENTA C ON C.id_cuenta = G.CUENTA_id
    ORDER BY G.clave;
END
GO

CREATE OR ALTER PROCEDURE CONTABILIDAD.GuardarCuentaConfig
    @Clave     VARCHAR(30),
    @IdCuenta  INT,
    @Mensaje   VARCHAR(500) OUTPUT,
    @Resultado BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Resultado = 0;
    IF NOT EXISTS (SELECT 1 FROM CONTABILIDAD.CUENTA WHERE id_cuenta = @IdCuenta AND imputable = 1 AND estado = 1)
    BEGIN
        SET @Mensaje = 'Seleccione una cuenta imputable y activa.';
        RETURN;
    END
    UPDATE CONTABILIDAD.CUENTA_CONFIG SET CUENTA_id = @IdCuenta WHERE clave = @Clave;
    SET @Resultado = 1;
    SET @Mensaje = 'Cuenta asignada.';
END
GO

-- ---------- Asientos ----------
CREATE OR ALTER PROCEDURE CONTABILIDAD.RegistrarAsiento
    @Fecha       DATE,
    @Glosa       VARCHAR(500),
    @Tipo        VARCHAR(15),
    @OrigenTipo  VARCHAR(30) = NULL,
    @OrigenId    INT = NULL,
    @Usuario     VARCHAR(100),
    @Detalle     CONTABILIDAD.DetalleAsientoTipo READONLY,
    @Mensaje     VARCHAR(500) OUTPUT,
    @Resultado   INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Resultado = 0;

    IF EXISTS (SELECT 1 FROM CONTABILIDAD.PERIODO_CERRADO WHERE gestion = YEAR(@Fecha) AND mes = MONTH(@Fecha))
    BEGIN
        SET @Mensaje = 'El mes ' + RIGHT('0' + CAST(MONTH(@Fecha) AS VARCHAR(2)), 2) + '/' + CAST(YEAR(@Fecha) AS VARCHAR(4)) + ' está cerrado.';
        RETURN;
    END
    IF (SELECT COUNT(*) FROM @Detalle) < 2
    BEGIN
        SET @Mensaje = 'El asiento necesita al menos dos líneas.';
        RETURN;
    END
    IF (SELECT SUM(debe) FROM @Detalle) <> (SELECT SUM(haber) FROM @Detalle)
    BEGIN
        SET @Mensaje = 'El asiento no cuadra: el total del debe es distinto del total del haber.';
        RETURN;
    END
    IF EXISTS (SELECT 1 FROM @Detalle T LEFT JOIN CONTABILIDAD.CUENTA C ON C.id_cuenta = T.CUENTA_id
               WHERE C.id_cuenta IS NULL OR C.imputable = 0 OR C.estado = 0)
    BEGIN
        SET @Mensaje = 'Todas las líneas deben usar cuentas imputables y activas.';
        RETURN;
    END

    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @Numero INT;
        SELECT @Numero = ISNULL(MAX(numero), 0) + 1 FROM CONTABILIDAD.ASIENTO WITH (UPDLOCK, HOLDLOCK) WHERE gestion = YEAR(@Fecha);

        INSERT INTO CONTABILIDAD.ASIENTO (gestion, numero, fecha, glosa, tipo, origen_tipo, origen_id, usuario)
        VALUES (YEAR(@Fecha), @Numero, @Fecha, @Glosa, @Tipo, @OrigenTipo, @OrigenId, @Usuario);
        DECLARE @Id INT = SCOPE_IDENTITY();

        INSERT INTO CONTABILIDAD.ASIENTO_DETALLE (ASIENTO_id, CUENTA_id, debe, haber, glosa)
        SELECT @Id, CUENTA_id, debe, haber, glosa FROM @Detalle WHERE debe + haber > 0;

        COMMIT TRANSACTION;
        SET @Resultado = @Id;
        SET @Mensaje = 'Asiento N° ' + CAST(@Numero AS VARCHAR(10)) + ' registrado.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        SET @Mensaje = 'Error al registrar el asiento: ' + ERROR_MESSAGE();
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE CONTABILIDAD.AnularAsiento
    @IdAsiento  INT,
    @Mensaje    VARCHAR(500) OUTPUT,
    @Resultado  BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Resultado = 0;
    DECLARE @Fecha DATE, @Tipo VARCHAR(15), @Estado VARCHAR(10);
    SELECT @Fecha = fecha, @Tipo = tipo, @Estado = estado FROM CONTABILIDAD.ASIENTO WHERE id_asiento = @IdAsiento;

    IF @Fecha IS NULL BEGIN SET @Mensaje = 'El asiento no existe.'; RETURN; END
    IF @Estado <> 'VIGENTE' BEGIN SET @Mensaje = 'El asiento ya está anulado.'; RETURN; END
    IF @Tipo <> 'MANUAL' BEGIN SET @Mensaje = 'Los asientos automáticos se corrigen desde su documento de origen (anulación de factura, nota...).'; RETURN; END
    IF EXISTS (SELECT 1 FROM CONTABILIDAD.PERIODO_CERRADO WHERE gestion = YEAR(@Fecha) AND mes = MONTH(@Fecha))
    BEGIN SET @Mensaje = 'El mes del asiento está cerrado.'; RETURN; END

    UPDATE CONTABILIDAD.ASIENTO SET estado = 'ANULADO' WHERE id_asiento = @IdAsiento;
    SET @Resultado = 1;
    SET @Mensaje = 'Asiento anulado.';
END
GO

CREATE OR ALTER PROCEDURE CONTABILIDAD.ListarAsientos
    @FechaInicio  DATE,
    @FechaFin     DATE,
    @IdAsiento    INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT A.id_asiento, A.gestion, A.numero, A.fecha, A.glosa, A.tipo, A.origen_tipo, A.origen_id, A.estado, A.usuario,
           D.id_asiento_detalle, D.debe, D.haber, D.glosa AS glosa_linea, C.id_cuenta, C.codigo, C.nombre
    FROM CONTABILIDAD.ASIENTO A
    INNER JOIN CONTABILIDAD.ASIENTO_DETALLE D ON D.ASIENTO_id = A.id_asiento
    INNER JOIN CONTABILIDAD.CUENTA C ON C.id_cuenta = D.CUENTA_id
    WHERE (@IdAsiento IS NOT NULL AND A.id_asiento = @IdAsiento)
       OR (@IdAsiento IS NULL AND A.fecha BETWEEN @FechaInicio AND @FechaFin)
    ORDER BY A.fecha, A.numero, D.id_asiento_detalle;
END
GO

-- Movimientos de una cuenta (Libro Mayor) con el saldo anterior al periodo.
CREATE OR ALTER PROCEDURE CONTABILIDAD.LibroMayor
    @IdCuenta     INT,
    @FechaInicio  DATE,
    @FechaFin     DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(SUM(D.debe), 0) AS debe, ISNULL(SUM(D.haber), 0) AS haber
    FROM CONTABILIDAD.ASIENTO_DETALLE D
    INNER JOIN CONTABILIDAD.ASIENTO A ON A.id_asiento = D.ASIENTO_id AND A.estado = 'VIGENTE'
    WHERE D.CUENTA_id = @IdCuenta AND A.fecha < @FechaInicio;

    SELECT A.id_asiento, A.numero, A.fecha, A.glosa, D.debe, D.haber, D.glosa AS glosa_linea
    FROM CONTABILIDAD.ASIENTO_DETALLE D
    INNER JOIN CONTABILIDAD.ASIENTO A ON A.id_asiento = D.ASIENTO_id AND A.estado = 'VIGENTE'
    WHERE D.CUENTA_id = @IdCuenta AND A.fecha BETWEEN @FechaInicio AND @FechaFin
    ORDER BY A.fecha, A.numero, D.id_asiento_detalle;
END
GO

-- Sumas por cuenta imputable (base del Balance de comprobación, Estado de resultados y Balance general).
CREATE OR ALTER PROCEDURE CONTABILIDAD.SumasPorCuenta
    @FechaInicio  DATE,
    @FechaFin     DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT C.id_cuenta, C.codigo, C.nombre, C.tipo, C.naturaleza,
           SUM(D.debe) AS debe, SUM(D.haber) AS haber
    FROM CONTABILIDAD.ASIENTO_DETALLE D
    INNER JOIN CONTABILIDAD.ASIENTO A ON A.id_asiento = D.ASIENTO_id AND A.estado = 'VIGENTE'
    INNER JOIN CONTABILIDAD.CUENTA C ON C.id_cuenta = D.CUENTA_id
    WHERE A.fecha BETWEEN @FechaInicio AND @FechaFin
    GROUP BY C.id_cuenta, C.codigo, C.nombre, C.tipo, C.naturaleza
    ORDER BY C.codigo;
END
GO

-- ---------- Periodos ----------
CREATE OR ALTER PROCEDURE CONTABILIDAD.ListarPeriodosCerrados
AS
BEGIN
    SET NOCOUNT ON;
    SELECT gestion, mes, usuario, fecha_cierre FROM CONTABILIDAD.PERIODO_CERRADO ORDER BY gestion DESC, mes DESC;
END
GO

CREATE OR ALTER PROCEDURE CONTABILIDAD.CambiarCierrePeriodo
    @Gestion  INT,
    @Mes      TINYINT,
    @Cerrar   BIT,
    @Usuario  VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    IF @Cerrar = 1 AND NOT EXISTS (SELECT 1 FROM CONTABILIDAD.PERIODO_CERRADO WHERE gestion = @Gestion AND mes = @Mes)
        INSERT INTO CONTABILIDAD.PERIODO_CERRADO (gestion, mes, usuario) VALUES (@Gestion, @Mes, @Usuario);
    IF @Cerrar = 0
        DELETE FROM CONTABILIDAD.PERIODO_CERRADO WHERE gestion = @Gestion AND mes = @Mes;
END
GO

-- ---------- Documentos pendientes de contabilizar ----------
/* Devuelve, para el rango, los documentos que todavía no tienen asiento vigente:
   1) facturas  2) anulaciones de factura  3) notas  4) anulaciones de nota  5) compras.
   El costo de las mercaderías se toma del último precio de compra del producto (sin IVA si esa compra
   tiene factura registrada). */
CREATE OR ALTER PROCEDURE CONTABILIDAD.DocumentosPendientes
    @FechaInicio  DATE,
    @FechaFin     DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- 1) Facturas válidas o anuladas emitidas en el rango
    SELECT F.id_factura AS id, CAST(F.fecha_emision AS DATE) AS fecha, F.numero_factura AS numero, F.nombre_razon_social AS tercero,
           F.monto_total, F.codigo_metodo_pago,
           ISNULL((SELECT SUM(D.sub_total) FROM FACTURACION.FACTURA_DETALLE D WHERE D.FACTURA_id_factura = F.id_factura AND D.codigo_producto LIKE 'P-%'), 0) AS monto_productos,
           ISNULL((SELECT SUM(D.cantidad * X.costo)
                   FROM FACTURACION.FACTURA_DETALLE D
                   CROSS APPLY (SELECT TOP 1 DC.precio_compra * CASE WHEN CF.COMPRA_id_compra IS NULL THEN 1 ELSE 0.87 END AS costo
                                FROM TRANSACCIONES.DETALLE_COMPRA DC
                                INNER JOIN TRANSACCIONES.COMPRA C ON C.id_compra = DC.COMPRA_id_compra
                                LEFT JOIN CONTABILIDAD.COMPRA_FISCAL CF ON CF.COMPRA_id_compra = C.id_compra
                                WHERE DC.PRODUCTO_id_producto = TRY_CAST(SUBSTRING(D.codigo_producto, 3, 20) AS INT)
                                  AND C.fecha <= CAST(F.fecha_emision AS DATE)
                                ORDER BY C.fecha DESC, C.id_compra DESC) X
                   WHERE D.FACTURA_id_factura = F.id_factura AND D.codigo_producto LIKE 'P-%'), 0) AS costo_productos
    FROM FACTURACION.FACTURA F
    WHERE F.estado IN ('VALIDADA', 'ANULADA')
      AND CAST(F.fecha_emision AS DATE) BETWEEN @FechaInicio AND @FechaFin
      AND NOT EXISTS (SELECT 1 FROM CONTABILIDAD.ASIENTO A WHERE A.origen_tipo = 'FACTURA' AND A.origen_id = F.id_factura AND A.estado = 'VIGENTE')
    ORDER BY F.fecha_emision;

    -- 2) Anulaciones de factura en el rango
    SELECT F.id_factura AS id, CAST(F.fecha_anulacion AS DATE) AS fecha, F.numero_factura AS numero
    FROM FACTURACION.FACTURA F
    WHERE F.estado = 'ANULADA' AND CAST(F.fecha_anulacion AS DATE) BETWEEN @FechaInicio AND @FechaFin
      AND NOT EXISTS (SELECT 1 FROM CONTABILIDAD.ASIENTO A WHERE A.origen_tipo = 'ANULACION_FACTURA' AND A.origen_id = F.id_factura AND A.estado = 'VIGENTE')
    ORDER BY F.fecha_anulacion;

    -- 3) Notas válidas o anuladas emitidas en el rango
    SELECT N.id_nota AS id, CAST(N.fecha_emision AS DATE) AS fecha, N.numero_nota AS numero, F.numero_factura,
           F.nombre_razon_social AS tercero, N.monto_total_devuelto, N.monto_efectivo_credito_debito, F.codigo_metodo_pago,
           N.reingreso_stock,
           ISNULL((SELECT SUM(D.sub_total) FROM FACTURACION.NOTA_DETALLE D WHERE D.NOTA_id_nota = N.id_nota AND D.codigo_detalle_transaccion = 2 AND D.codigo_producto LIKE 'P-%'), 0) AS monto_productos,
           ISNULL((SELECT SUM(D.cantidad * X.costo)
                   FROM FACTURACION.NOTA_DETALLE D
                   CROSS APPLY (SELECT TOP 1 DC.precio_compra * CASE WHEN CF.COMPRA_id_compra IS NULL THEN 1 ELSE 0.87 END AS costo
                                FROM TRANSACCIONES.DETALLE_COMPRA DC
                                INNER JOIN TRANSACCIONES.COMPRA C ON C.id_compra = DC.COMPRA_id_compra
                                LEFT JOIN CONTABILIDAD.COMPRA_FISCAL CF ON CF.COMPRA_id_compra = C.id_compra
                                WHERE DC.PRODUCTO_id_producto = TRY_CAST(SUBSTRING(D.codigo_producto, 3, 20) AS INT)
                                  AND C.fecha <= CAST(F.fecha_emision AS DATE)
                                ORDER BY C.fecha DESC, C.id_compra DESC) X
                   WHERE D.NOTA_id_nota = N.id_nota AND D.codigo_detalle_transaccion = 2 AND D.codigo_producto LIKE 'P-%'), 0) AS costo_productos
    FROM FACTURACION.NOTA_CREDITO_DEBITO N
    INNER JOIN FACTURACION.FACTURA F ON F.id_factura = N.FACTURA_id_factura
    WHERE N.estado IN ('VALIDADA', 'ANULADA')
      AND CAST(N.fecha_emision AS DATE) BETWEEN @FechaInicio AND @FechaFin
      AND NOT EXISTS (SELECT 1 FROM CONTABILIDAD.ASIENTO A WHERE A.origen_tipo = 'NOTA' AND A.origen_id = N.id_nota AND A.estado = 'VIGENTE')
    ORDER BY N.fecha_emision;

    -- 4) Anulaciones de nota en el rango
    SELECT N.id_nota AS id, CAST(N.fecha_anulacion AS DATE) AS fecha, N.numero_nota AS numero
    FROM FACTURACION.NOTA_CREDITO_DEBITO N
    WHERE N.estado = 'ANULADA' AND CAST(N.fecha_anulacion AS DATE) BETWEEN @FechaInicio AND @FechaFin
      AND NOT EXISTS (SELECT 1 FROM CONTABILIDAD.ASIENTO A WHERE A.origen_tipo = 'ANULACION_NOTA' AND A.origen_id = N.id_nota AND A.estado = 'VIGENTE')
    ORDER BY N.fecha_anulacion;

    -- 5) Compras en el rango
    SELECT C.id_compra AS id, C.fecha, P.nombre AS tercero, C.monto_total, H.codigo_metodo_pago,
           CAST(CASE WHEN CF.COMPRA_id_compra IS NULL THEN 0 ELSE 1 END AS BIT) AS con_factura,
           CF.numero_factura, ISNULL(CF.tipo_compra, 0) AS tipo_compra,
           ISNULL(CF.importe_total - CF.importe_no_sujeto - CF.descuentos, 0) AS base_credito_fiscal
    FROM TRANSACCIONES.COMPRA C
    INNER JOIN COMERCIAL.PROVEEDOR P ON P.id_proveedor = C.PROVEEDOR_id_proveedor
    LEFT JOIN FACTURACION.HOMOLOGACION_TIPO_PAGO H ON H.TIPO_PAGO_id_tipo_pago = C.TIPO_PAGO_id_tipo_pago
    LEFT JOIN CONTABILIDAD.COMPRA_FISCAL CF ON CF.COMPRA_id_compra = C.id_compra
    WHERE C.fecha BETWEEN @FechaInicio AND @FechaFin
      AND NOT EXISTS (SELECT 1 FROM CONTABILIDAD.ASIENTO A WHERE A.origen_tipo = 'COMPRA' AND A.origen_id = C.id_compra AND A.estado = 'VIGENTE')
    ORDER BY C.fecha, C.id_compra;
END
GO

-- ---------- Datos fiscales de compras ----------
CREATE OR ALTER PROCEDURE CONTABILIDAD.ListarCompras
    @FechaInicio  DATE,
    @FechaFin     DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT C.id_compra, C.fecha, C.monto_total, P.nombre AS proveedor, P.nit AS nit_proveedor_registrado,
           CF.nit_proveedor, CF.razon_social, CF.codigo_autorizacion, CF.numero_factura, CF.fecha_factura,
           CF.importe_total, CF.importe_no_sujeto, CF.descuentos, CF.tipo_compra, CF.codigo_control,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM CONTABILIDAD.ASIENTO A WHERE A.origen_tipo = 'COMPRA' AND A.origen_id = C.id_compra AND A.estado = 'VIGENTE') THEN 1 ELSE 0 END AS BIT) AS contabilizada
    FROM TRANSACCIONES.COMPRA C
    INNER JOIN COMERCIAL.PROVEEDOR P ON P.id_proveedor = C.PROVEEDOR_id_proveedor
    LEFT JOIN CONTABILIDAD.COMPRA_FISCAL CF ON CF.COMPRA_id_compra = C.id_compra
    WHERE C.fecha BETWEEN @FechaInicio AND @FechaFin
    ORDER BY C.fecha, C.id_compra;
END
GO

CREATE OR ALTER PROCEDURE CONTABILIDAD.GuardarCompraFiscal
    @IdCompra            INT,
    @NitProveedor        BIGINT,
    @RazonSocial         VARCHAR(240),
    @CodigoAutorizacion  VARCHAR(100),
    @NumeroFactura       BIGINT,
    @FechaFactura        DATE,
    @ImporteTotal        DECIMAL(17,2),
    @ImporteNoSujeto     DECIMAL(17,2),
    @Descuentos          DECIMAL(17,2),
    @TipoCompra          TINYINT,
    @CodigoControl       VARCHAR(17) = NULL,
    @Mensaje             VARCHAR(500) OUTPUT,
    @Resultado           BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Resultado = 0;
    DECLARE @FechaCompra DATE = (SELECT fecha FROM TRANSACCIONES.COMPRA WHERE id_compra = @IdCompra);
    IF @FechaCompra IS NULL BEGIN SET @Mensaje = 'La compra no existe.'; RETURN; END
    IF EXISTS (SELECT 1 FROM CONTABILIDAD.PERIODO_CERRADO WHERE gestion = YEAR(@FechaCompra) AND mes = MONTH(@FechaCompra))
    BEGIN SET @Mensaje = 'El mes de la compra está cerrado.'; RETURN; END

    BEGIN TRANSACTION;
        IF EXISTS (SELECT 1 FROM CONTABILIDAD.COMPRA_FISCAL WHERE COMPRA_id_compra = @IdCompra)
            UPDATE CONTABILIDAD.COMPRA_FISCAL
            SET nit_proveedor = @NitProveedor, razon_social = @RazonSocial, codigo_autorizacion = @CodigoAutorizacion,
                numero_factura = @NumeroFactura, fecha_factura = @FechaFactura, importe_total = @ImporteTotal,
                importe_no_sujeto = @ImporteNoSujeto, descuentos = @Descuentos, tipo_compra = @TipoCompra, codigo_control = @CodigoControl
            WHERE COMPRA_id_compra = @IdCompra;
        ELSE
            INSERT INTO CONTABILIDAD.COMPRA_FISCAL (COMPRA_id_compra, nit_proveedor, razon_social, codigo_autorizacion, numero_factura,
                                                    fecha_factura, importe_total, importe_no_sujeto, descuentos, tipo_compra, codigo_control)
            VALUES (@IdCompra, @NitProveedor, @RazonSocial, @CodigoAutorizacion, @NumeroFactura, @FechaFactura, @ImporteTotal,
                    @ImporteNoSujeto, @Descuentos, @TipoCompra, @CodigoControl);

        -- Si la compra ya estaba contabilizada, su asiento se anula para regenerarlo con el crédito fiscal.
        UPDATE CONTABILIDAD.ASIENTO SET estado = 'ANULADO'
        WHERE origen_tipo = 'COMPRA' AND origen_id = @IdCompra AND estado = 'VIGENTE';
    COMMIT TRANSACTION;

    SET @Resultado = 1;
    SET @Mensaje = 'Datos de la factura del proveedor guardados.';
END
GO

-- ---------- Libros fiscales ----------
CREATE OR ALTER PROCEDURE CONTABILIDAD.LibroVentas
    @Gestion  INT,
    @Mes      TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT F.id_factura, F.fecha_emision, F.numero_factura, F.cuf, F.numero_documento, F.complemento, F.nombre_razon_social,
           F.monto_total, F.descuento_adicional, F.monto_total_sujeto_iva, F.estado, F.tipo_emision
    FROM FACTURACION.FACTURA F
    WHERE F.estado IN ('VALIDADA', 'ANULADA')
      AND YEAR(F.fecha_emision) = @Gestion AND MONTH(F.fecha_emision) = @Mes
    ORDER BY F.fecha_emision, F.numero_factura;
END
GO

CREATE OR ALTER PROCEDURE CONTABILIDAD.LibroCompras
    @Gestion  INT,
    @Mes      TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CF.COMPRA_id_compra AS id_compra, CF.nit_proveedor, CF.razon_social, CF.codigo_autorizacion, CF.numero_factura,
           CF.fecha_factura, CF.importe_total, CF.importe_no_sujeto, CF.descuentos, CF.tipo_compra, CF.codigo_control
    FROM CONTABILIDAD.COMPRA_FISCAL CF
    WHERE YEAR(CF.fecha_factura) = @Gestion AND MONTH(CF.fecha_factura) = @Mes
    ORDER BY CF.fecha_factura, CF.COMPRA_id_compra;
END
GO

/* Totales mensuales para la liquidación de IVA e IT, desde el primer mes con movimiento hasta el pedido. */
CREATE OR ALTER PROCEDURE CONTABILIDAD.TotalesImpositivos
    @Gestion  INT,
    @Mes      TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Fin DATE = EOMONTH(DATEFROMPARTS(@Gestion, @Mes, 1));

    ;WITH ventas AS (
        SELECT YEAR(fecha_emision) AS g, MONTH(fecha_emision) AS m, SUM(monto_total_sujeto_iva) AS base_df
        FROM FACTURACION.FACTURA WHERE estado = 'VALIDADA' AND fecha_emision <= @Fin
        GROUP BY YEAR(fecha_emision), MONTH(fecha_emision)
    ), notas AS (
        SELECT YEAR(fecha_emision) AS g, MONTH(fecha_emision) AS m, SUM(monto_total_devuelto) AS devuelto,
               SUM(monto_efectivo_credito_debito) AS cf_devoluciones
        FROM FACTURACION.NOTA_CREDITO_DEBITO WHERE estado = 'VALIDADA' AND fecha_emision <= @Fin
        GROUP BY YEAR(fecha_emision), MONTH(fecha_emision)
    ), compras AS (
        SELECT YEAR(fecha_factura) AS g, MONTH(fecha_factura) AS m, SUM(importe_total - importe_no_sujeto - descuentos) AS base_cf
        FROM CONTABILIDAD.COMPRA_FISCAL WHERE tipo_compra = 1 AND fecha_factura <= @Fin
        GROUP BY YEAR(fecha_factura), MONTH(fecha_factura)
    ), meses AS (
        SELECT g, m FROM ventas UNION SELECT g, m FROM notas UNION SELECT g, m FROM compras
        UNION SELECT @Gestion, @Mes
    )
    SELECT M.g AS gestion, M.m AS mes, ISNULL(V.base_df, 0) AS base_debito_fiscal, ISNULL(N.devuelto, 0) AS devoluciones,
           ISNULL(N.cf_devoluciones, 0) AS credito_fiscal_devoluciones, ISNULL(C.base_cf, 0) AS base_credito_fiscal
    FROM meses M
    LEFT JOIN ventas V ON V.g = M.g AND V.m = M.m
    LEFT JOIN notas N ON N.g = M.g AND N.m = M.m
    LEFT JOIN compras C ON C.g = M.g AND C.m = M.m
    ORDER BY M.g, M.m;
END
GO

PRINT 'Esquema CONTABILIDAD instalado correctamente.';
GO
