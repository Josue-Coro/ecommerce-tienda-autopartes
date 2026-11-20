/* =====================================================================================
   MÓDULO DE FACTURACIÓN - Esquema FACTURACION
   Basado en el Anexo Técnico del Sistema de Facturación del SIN (RND 102100000011).
   Modalidad: Computarizada en Línea, documento sector 1 (Factura Compra Venta).

   - No modifica ninguna tabla existente: toda la información nueva vive en el esquema
     FACTURACION y se relaciona con las tablas actuales mediante claves foráneas.
   - Es idempotente: puede ejecutarse varias veces.
   - Requiere SQL Server 2016 SP1 o superior (usa CREATE OR ALTER).
   ===================================================================================== */

USE [DOSHERMANOS]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'FACTURACION')
    EXEC('CREATE SCHEMA [FACTURACION]');
GO

/* -------------------------------------------------------------------------------------
   1. CONFIGURACIÓN DEL EMISOR (una sola fila)
   ------------------------------------------------------------------------------------- */
IF OBJECT_ID('FACTURACION.CONFIGURACION', 'U') IS NULL
BEGIN
    CREATE TABLE FACTURACION.CONFIGURACION (
        id_configuracion     INT          NOT NULL CONSTRAINT CONFIGURACION_PK PRIMARY KEY
                                          CONSTRAINT CONFIGURACION_CK_UNICA CHECK (id_configuracion = 1),
        nit                  BIGINT       NOT NULL,
        razon_social         VARCHAR(200) NOT NULL,
        codigo_sistema       VARCHAR(50)  NOT NULL,
        codigo_ambiente      TINYINT      NOT NULL CONSTRAINT CONFIGURACION_DF_AMBIENTE DEFAULT (2),   -- 1 Producción, 2 Piloto
        codigo_modalidad     TINYINT      NOT NULL CONSTRAINT CONFIGURACION_DF_MODALIDAD DEFAULT (2),  -- 1 Electrónica, 2 Computarizada
        actividad_economica  VARCHAR(10)  NOT NULL,
        modo_simulado        BIT          NOT NULL CONSTRAINT CONFIGURACION_DF_SIMULADO DEFAULT (1),
        token_delegado       VARCHAR(MAX) NULL,
        fecha_actualizacion  DATETIME     NOT NULL CONSTRAINT CONFIGURACION_DF_FECHA DEFAULT (GETDATE())
    );

    -- Datos de ejemplo (ficticios) para trabajar en modo simulado.
    INSERT INTO FACTURACION.CONFIGURACION (id_configuracion, nit, razon_social, codigo_sistema, codigo_ambiente,
                                           codigo_modalidad, actividad_economica, modo_simulado)
    VALUES (1, 1020304050, 'AUTOPARTES DOS HERMANOS', 'SIM-DOSHERMANOS-01', 2, 2, '453000', 1);
END
GO

/* -------------------------------------------------------------------------------------
   2. SUCURSALES / PUNTOS DE VENTA (relación TIENDA -> Padrón del SIN)
   ------------------------------------------------------------------------------------- */
IF OBJECT_ID('FACTURACION.SUCURSAL', 'U') IS NULL
BEGIN
    CREATE TABLE FACTURACION.SUCURSAL (
        TIENDA_id_tienda    INT         NOT NULL CONSTRAINT SUCURSAL_PK PRIMARY KEY,
        codigo_sucursal     INT         NOT NULL,   -- 0 = Casa Matriz
        codigo_punto_venta  INT         NOT NULL CONSTRAINT SUCURSAL_DF_PV DEFAULT (0),
        municipio           VARCHAR(25) NOT NULL,
        telefono            VARCHAR(25) NULL,
        CONSTRAINT SUCURSAL_TIENDA_FK FOREIGN KEY (TIENDA_id_tienda) REFERENCES INVENTARIO.TIENDA (id_tienda),
        CONSTRAINT SUCURSAL_UQ_CODIGOS UNIQUE (codigo_sucursal, codigo_punto_venta)
    );
END
GO

/* -------------------------------------------------------------------------------------
   3. CÓDIGOS DE AUTORIZACIÓN: CUIS (365 días) y CUFD (24 horas)
   ------------------------------------------------------------------------------------- */
IF OBJECT_ID('FACTURACION.CUIS', 'U') IS NULL
BEGIN
    CREATE TABLE FACTURACION.CUIS (
        id_cuis           INT IDENTITY(1,1) NOT NULL CONSTRAINT CUIS_PK PRIMARY KEY,
        TIENDA_id_tienda  INT          NOT NULL,
        codigo            VARCHAR(100) NOT NULL,
        fecha_solicitud   DATETIME     NOT NULL CONSTRAINT CUIS_DF_FECHA DEFAULT (GETDATE()),
        fecha_vigencia    DATETIME     NOT NULL,
        activo            BIT          NOT NULL CONSTRAINT CUIS_DF_ACTIVO DEFAULT (1),
        CONSTRAINT CUIS_SUCURSAL_FK FOREIGN KEY (TIENDA_id_tienda) REFERENCES FACTURACION.SUCURSAL (TIENDA_id_tienda)
    );
END
GO

IF OBJECT_ID('FACTURACION.CUFD', 'U') IS NULL
BEGIN
    CREATE TABLE FACTURACION.CUFD (
        id_cufd           INT IDENTITY(1,1) NOT NULL CONSTRAINT CUFD_PK PRIMARY KEY,
        TIENDA_id_tienda  INT          NOT NULL,
        codigo            VARCHAR(100) NOT NULL,
        codigo_control    VARCHAR(50)  NOT NULL,
        direccion         VARCHAR(500) NULL,
        fecha_solicitud   DATETIME     NOT NULL CONSTRAINT CUFD_DF_FECHA DEFAULT (GETDATE()),
        fecha_vigencia    DATETIME     NOT NULL,
        activo            BIT          NOT NULL CONSTRAINT CUFD_DF_ACTIVO DEFAULT (1),
        CONSTRAINT CUFD_SUCURSAL_FK FOREIGN KEY (TIENDA_id_tienda) REFERENCES FACTURACION.SUCURSAL (TIENDA_id_tienda)
    );
END
GO

/* -------------------------------------------------------------------------------------
   4. CATÁLOGOS DEL SIN (paramétricas sincronizadas a diario)
   ------------------------------------------------------------------------------------- */
IF OBJECT_ID('FACTURACION.CATALOGO', 'U') IS NULL
BEGIN
    CREATE TABLE FACTURACION.CATALOGO (
        id_catalogo           INT IDENTITY(1,1) NOT NULL CONSTRAINT CATALOGO_PK PRIMARY KEY,
        tipo                  VARCHAR(40)  NOT NULL,
        codigo                VARCHAR(20)  NOT NULL,
        descripcion           VARCHAR(500) NOT NULL,
        codigo_actividad      VARCHAR(10)  NULL,
        fecha_sincronizacion  DATETIME     NOT NULL CONSTRAINT CATALOGO_DF_FECHA DEFAULT (GETDATE()),
        CONSTRAINT CATALOGO_UQ_TIPO_CODIGO UNIQUE (tipo, codigo)
    );
END
GO

IF TYPE_ID('FACTURACION.CatalogoTipo') IS NULL
    CREATE TYPE FACTURACION.CatalogoTipo AS TABLE (
        codigo            VARCHAR(20)  NOT NULL,
        descripcion       VARCHAR(500) NOT NULL,
        codigo_actividad  VARCHAR(10)  NULL
    );
GO

/* -------------------------------------------------------------------------------------
   5. HOMOLOGACIÓN (registros propios -> códigos del SIN)
   ------------------------------------------------------------------------------------- */
IF OBJECT_ID('FACTURACION.HOMOLOGACION_PRODUCTO', 'U') IS NULL
    CREATE TABLE FACTURACION.HOMOLOGACION_PRODUCTO (
        PRODUCTO_id_producto  INT NOT NULL CONSTRAINT HOMOLOGACION_PRODUCTO_PK PRIMARY KEY,
        codigo_producto_sin   INT NOT NULL,
        CONSTRAINT HOMOLOGACION_PRODUCTO_FK FOREIGN KEY (PRODUCTO_id_producto) REFERENCES INVENTARIO.PRODUCTO (id_producto)
    );
GO

IF OBJECT_ID('FACTURACION.HOMOLOGACION_SERVICIO', 'U') IS NULL
    CREATE TABLE FACTURACION.HOMOLOGACION_SERVICIO (
        SERVICIO_id_servicio  INT NOT NULL CONSTRAINT HOMOLOGACION_SERVICIO_PK PRIMARY KEY,
        codigo_producto_sin   INT NOT NULL,
        CONSTRAINT HOMOLOGACION_SERVICIO_FK FOREIGN KEY (SERVICIO_id_servicio) REFERENCES TRANSACCIONES.SERVICIO (id_servicio)
    );
GO

IF OBJECT_ID('FACTURACION.HOMOLOGACION_UNIDAD_MEDIDA', 'U') IS NULL
    CREATE TABLE FACTURACION.HOMOLOGACION_UNIDAD_MEDIDA (
        UNIDAD_MEDIDA_id_unidad_medida  INT NOT NULL CONSTRAINT HOMOLOGACION_UNIDAD_PK PRIMARY KEY,
        codigo_unidad_medida            INT NOT NULL,
        CONSTRAINT HOMOLOGACION_UNIDAD_FK FOREIGN KEY (UNIDAD_MEDIDA_id_unidad_medida) REFERENCES INVENTARIO.UNIDAD_MEDIDA (id_unidad_medida)
    );
GO

IF OBJECT_ID('FACTURACION.HOMOLOGACION_TIPO_PAGO', 'U') IS NULL
    CREATE TABLE FACTURACION.HOMOLOGACION_TIPO_PAGO (
        TIPO_PAGO_id_tipo_pago  INT NOT NULL CONSTRAINT HOMOLOGACION_TIPO_PAGO_PK PRIMARY KEY,
        codigo_metodo_pago      INT NOT NULL,
        CONSTRAINT HOMOLOGACION_TIPO_PAGO_FK FOREIGN KEY (TIPO_PAGO_id_tipo_pago) REFERENCES TRANSACCIONES.TIPO_PAGO (id_tipo_pago)
    );
GO

/* -------------------------------------------------------------------------------------
   6. DATOS FISCALES DEL CLIENTE (CI / NIT / razón social)
   ------------------------------------------------------------------------------------- */
IF OBJECT_ID('FACTURACION.CLIENTE_FISCAL', 'U') IS NULL
    CREATE TABLE FACTURACION.CLIENTE_FISCAL (
        CLIENTE_id_cliente     INT          NOT NULL CONSTRAINT CLIENTE_FISCAL_PK PRIMARY KEY,
        codigo_tipo_documento  INT          NOT NULL,   -- 1 CI, 2 CEX, 3 PAS, 4 OD, 5 NIT
        numero_documento       VARCHAR(20)  NOT NULL,
        complemento            VARCHAR(5)   NULL,
        razon_social           VARCHAR(500) NOT NULL,
        correo                 VARCHAR(100) NULL,
        CONSTRAINT CLIENTE_FISCAL_FK FOREIGN KEY (CLIENTE_id_cliente) REFERENCES COMERCIAL.CLIENTE (id_cliente)
    );
GO

/* -------------------------------------------------------------------------------------
   7. NUMERACIÓN Y FACTURAS
   ------------------------------------------------------------------------------------- */
IF OBJECT_ID('FACTURACION.NUMERACION', 'U') IS NULL
    CREATE TABLE FACTURACION.NUMERACION (
        TIENDA_id_tienda  INT    NOT NULL CONSTRAINT NUMERACION_PK PRIMARY KEY,
        ultimo_numero     BIGINT NOT NULL CONSTRAINT NUMERACION_DF DEFAULT (0),
        CONSTRAINT NUMERACION_SUCURSAL_FK FOREIGN KEY (TIENDA_id_tienda) REFERENCES FACTURACION.SUCURSAL (TIENDA_id_tienda)
    );
GO

IF OBJECT_ID('FACTURACION.FACTURA', 'U') IS NULL
BEGIN
    CREATE TABLE FACTURACION.FACTURA (
        id_factura               INT IDENTITY(1,1) NOT NULL CONSTRAINT FACTURA_PK PRIMARY KEY,
        VENTA_id_venta           INT           NOT NULL,
        TIENDA_id_tienda         INT           NOT NULL,
        -- Emisor
        nit_emisor               BIGINT        NOT NULL,
        razon_social_emisor      VARCHAR(200)  NOT NULL,
        municipio                VARCHAR(25)   NOT NULL,
        telefono                 VARCHAR(25)   NULL,
        direccion                VARCHAR(500)  NOT NULL,
        codigo_sucursal          INT           NOT NULL,
        codigo_punto_venta       INT           NOT NULL,
        -- Identificación
        numero_factura           BIGINT        NOT NULL,
        cuf                      VARCHAR(100)  NOT NULL,
        cufd                     VARCHAR(100)  NOT NULL,
        fecha_emision            DATETIME2(3)  NOT NULL,
        codigo_modalidad         TINYINT       NOT NULL,
        tipo_emision             TINYINT       NOT NULL,   -- 1 En línea, 2 Fuera de línea
        tipo_factura             TINYINT       NOT NULL,   -- 1 Con derecho a crédito fiscal
        codigo_documento_sector  INT           NOT NULL,   -- 1 Compra Venta
        -- Comprador
        nombre_razon_social      VARCHAR(500)  NOT NULL,
        codigo_tipo_documento    INT           NOT NULL,
        numero_documento         VARCHAR(20)   NOT NULL,
        complemento              VARCHAR(5)    NULL,
        codigo_cliente           VARCHAR(100)  NOT NULL,
        correo_cliente           VARCHAR(100)  NULL,
        -- Montos
        codigo_metodo_pago       INT           NOT NULL,
        numero_tarjeta           VARCHAR(16)   NULL,
        monto_total              DECIMAL(17,2) NOT NULL,
        monto_total_sujeto_iva   DECIMAL(17,2) NOT NULL,
        descuento_adicional      DECIMAL(17,2) NOT NULL CONSTRAINT FACTURA_DF_DESCUENTO DEFAULT (0),
        codigo_moneda            INT           NOT NULL,
        tipo_cambio              DECIMAL(17,2) NOT NULL,
        monto_total_moneda       DECIMAL(17,2) NOT NULL,
        codigo_excepcion         TINYINT       NOT NULL CONSTRAINT FACTURA_DF_EXCEPCION DEFAULT (0),
        leyenda                  VARCHAR(200)  NOT NULL,
        usuario                  VARCHAR(100)  NOT NULL,
        -- Envío al SIN
        archivo_xml              NVARCHAR(MAX) NOT NULL,
        hash_archivo             VARCHAR(64)   NOT NULL,
        codigo_recepcion         VARCHAR(100)  NULL,
        estado                   VARCHAR(20)   NOT NULL,   -- VALIDADA, OBSERVADA, PENDIENTE, ANULADA
        codigo_estado            INT           NOT NULL,
        mensajes                 VARCHAR(MAX)  NULL,
        -- Anulación
        fecha_anulacion          DATETIME      NULL,
        codigo_motivo_anulacion  INT           NULL,
        fecha_registro           DATETIME      NOT NULL CONSTRAINT FACTURA_DF_REGISTRO DEFAULT (GETDATE()),
        CONSTRAINT FACTURA_VENTA_FK    FOREIGN KEY (VENTA_id_venta)   REFERENCES TRANSACCIONES.VENTA (id_venta),
        CONSTRAINT FACTURA_SUCURSAL_FK FOREIGN KEY (TIENDA_id_tienda) REFERENCES FACTURACION.SUCURSAL (TIENDA_id_tienda),
        CONSTRAINT FACTURA_UQ_CUF UNIQUE (cuf),
        CONSTRAINT FACTURA_UQ_NUMERO UNIQUE (TIENDA_id_tienda, numero_factura)
    );

    -- Una venta solo puede tener una factura vigente (validada o pendiente).
    CREATE UNIQUE INDEX FACTURA_UQ_VENTA_VIGENTE ON FACTURACION.FACTURA (VENTA_id_venta)
        WHERE estado IN ('VALIDADA', 'PENDIENTE');
END
GO

IF OBJECT_ID('FACTURACION.FACTURA_DETALLE', 'U') IS NULL
    CREATE TABLE FACTURACION.FACTURA_DETALLE (
        id_factura_detalle   INT IDENTITY(1,1) NOT NULL CONSTRAINT FACTURA_DETALLE_PK PRIMARY KEY,
        FACTURA_id_factura   INT           NOT NULL,
        actividad_economica  VARCHAR(10)   NOT NULL,
        codigo_producto_sin  INT           NOT NULL,
        codigo_producto      VARCHAR(50)   NOT NULL,
        descripcion          VARCHAR(500)  NOT NULL,
        cantidad             DECIMAL(17,2) NOT NULL,
        unidad_medida        INT           NOT NULL,
        precio_unitario      DECIMAL(17,2) NOT NULL,
        monto_descuento      DECIMAL(17,2) NOT NULL CONSTRAINT FACTURA_DETALLE_DF_DESC DEFAULT (0),
        sub_total            DECIMAL(17,2) NOT NULL,
        numero_serie         VARCHAR(1500) NULL,
        CONSTRAINT FACTURA_DETALLE_FACTURA_FK FOREIGN KEY (FACTURA_id_factura) REFERENCES FACTURACION.FACTURA (id_factura)
    );
GO

IF TYPE_ID('FACTURACION.DetalleFacturaTipo') IS NULL
    CREATE TYPE FACTURACION.DetalleFacturaTipo AS TABLE (
        actividad_economica  VARCHAR(10)   NOT NULL,
        codigo_producto_sin  INT           NOT NULL,
        codigo_producto      VARCHAR(50)   NOT NULL,
        descripcion          VARCHAR(500)  NOT NULL,
        cantidad             DECIMAL(17,2) NOT NULL,
        unidad_medida        INT           NOT NULL,
        precio_unitario      DECIMAL(17,2) NOT NULL,
        monto_descuento      DECIMAL(17,2) NOT NULL,
        sub_total            DECIMAL(17,2) NOT NULL,
        numero_serie         VARCHAR(1500) NULL
    );
GO

/* -------------------------------------------------------------------------------------
   8. CONTINGENCIA: EVENTOS SIGNIFICATIVOS Y PAQUETES (emisión fuera de línea)
   ------------------------------------------------------------------------------------- */
IF COL_LENGTH('FACTURACION.CONFIGURACION', 'simular_sin_conexion') IS NULL
    ALTER TABLE FACTURACION.CONFIGURACION
        ADD simular_sin_conexion BIT NOT NULL CONSTRAINT CONFIGURACION_DF_SIN_CONEXION DEFAULT (0);
GO

IF OBJECT_ID('FACTURACION.EVENTO_SIGNIFICATIVO', 'U') IS NULL
    CREATE TABLE FACTURACION.EVENTO_SIGNIFICATIVO (
        id_evento                INT IDENTITY(1,1) NOT NULL CONSTRAINT EVENTO_PK PRIMARY KEY,
        TIENDA_id_tienda         INT           NOT NULL,
        codigo_evento            INT           NOT NULL,   -- catálogo EVENTO_SIGNIFICATIVO (1..7)
        descripcion              VARCHAR(500)  NOT NULL,
        fecha_inicio             DATETIME2(3)  NOT NULL,
        fecha_fin                DATETIME2(3)  NULL,
        cufd_evento              VARCHAR(100)  NOT NULL,   -- CUFD vigente al ingresar en contingencia
        codigo_control_evento    VARCHAR(50)   NOT NULL,
        automatico               BIT           NOT NULL CONSTRAINT EVENTO_DF_AUTOMATICO DEFAULT (0),
        estado                   VARCHAR(20)   NOT NULL,   -- ABIERTO, REGISTRADO, VALIDADO, OBSERVADO
        codigo_recepcion_evento  VARCHAR(100)  NULL,
        mensajes                 VARCHAR(MAX)  NULL,
        CONSTRAINT EVENTO_SUCURSAL_FK FOREIGN KEY (TIENDA_id_tienda) REFERENCES FACTURACION.SUCURSAL (TIENDA_id_tienda)
    );
GO

-- Solo puede haber un evento abierto por tienda.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'EVENTO_UQ_ABIERTO')
    CREATE UNIQUE INDEX EVENTO_UQ_ABIERTO ON FACTURACION.EVENTO_SIGNIFICATIVO (TIENDA_id_tienda) WHERE estado = 'ABIERTO';
GO

IF COL_LENGTH('FACTURACION.FACTURA', 'EVENTO_id_evento') IS NULL
BEGIN
    ALTER TABLE FACTURACION.FACTURA ADD EVENTO_id_evento INT NULL;
    ALTER TABLE FACTURACION.FACTURA ADD CONSTRAINT FACTURA_EVENTO_FK
        FOREIGN KEY (EVENTO_id_evento) REFERENCES FACTURACION.EVENTO_SIGNIFICATIVO (id_evento);
END
GO

IF OBJECT_ID('FACTURACION.PAQUETE', 'U') IS NULL
    CREATE TABLE FACTURACION.PAQUETE (
        id_paquete         INT IDENTITY(1,1) NOT NULL CONSTRAINT PAQUETE_PK PRIMARY KEY,
        EVENTO_id_evento   INT           NOT NULL,
        cantidad_facturas  INT           NOT NULL,
        hash_archivo       VARCHAR(64)   NOT NULL,
        codigo_recepcion   VARCHAR(100)  NULL,
        codigo_estado      INT           NOT NULL,
        estado             VARCHAR(20)   NOT NULL,   -- VALIDADO, OBSERVADO, RECHAZADO
        mensajes           VARCHAR(MAX)  NULL,
        fecha_envio        DATETIME      NOT NULL CONSTRAINT PAQUETE_DF_FECHA DEFAULT (GETDATE()),
        CONSTRAINT PAQUETE_EVENTO_FK FOREIGN KEY (EVENTO_id_evento) REFERENCES FACTURACION.EVENTO_SIGNIFICATIVO (id_evento)
    );
GO

/* -------------------------------------------------------------------------------------
   9. NOTAS DE CRÉDITO-DÉBITO (documento sector 24, documento de ajuste)
   ------------------------------------------------------------------------------------- */
IF COL_LENGTH('FACTURACION.NUMERACION', 'ultimo_numero_nota') IS NULL
    ALTER TABLE FACTURACION.NUMERACION ADD ultimo_numero_nota BIGINT NOT NULL CONSTRAINT NUMERACION_DF_NOTA DEFAULT (0);
GO

IF OBJECT_ID('FACTURACION.NOTA_CREDITO_DEBITO', 'U') IS NULL
    CREATE TABLE FACTURACION.NOTA_CREDITO_DEBITO (
        id_nota                       INT IDENTITY(1,1) NOT NULL CONSTRAINT NOTA_PK PRIMARY KEY,
        FACTURA_id_factura            INT           NOT NULL,
        TIENDA_id_tienda              INT           NOT NULL,
        numero_nota                   BIGINT        NOT NULL,
        cuf                           VARCHAR(100)  NOT NULL,
        cufd                          VARCHAR(100)  NOT NULL,
        fecha_emision                 DATETIME2(3)  NOT NULL,
        codigo_sucursal               INT           NOT NULL,
        codigo_punto_venta            INT           NOT NULL,
        direccion                     VARCHAR(500)  NOT NULL,
        municipio                     VARCHAR(25)   NOT NULL,
        telefono                      VARCHAR(25)   NULL,
        monto_total_original          DECIMAL(17,2) NOT NULL,
        monto_total_devuelto          DECIMAL(17,2) NOT NULL,
        monto_efectivo_credito_debito DECIMAL(17,2) NOT NULL,   -- 13 % del monto devuelto
        codigo_excepcion              TINYINT       NOT NULL CONSTRAINT NOTA_DF_EXCEPCION DEFAULT (0),
        leyenda                       VARCHAR(200)  NOT NULL,
        usuario                       VARCHAR(100)  NOT NULL,
        motivo                        VARCHAR(500)  NOT NULL,   -- motivo interno de la devolución
        reingreso_stock               BIT           NOT NULL,
        archivo_xml                   NVARCHAR(MAX) NOT NULL,
        hash_archivo                  VARCHAR(64)   NOT NULL,
        codigo_recepcion              VARCHAR(100)  NULL,
        estado                        VARCHAR(20)   NOT NULL,   -- VALIDADA, OBSERVADA, ANULADA
        codigo_estado                 INT           NOT NULL,
        mensajes                      VARCHAR(MAX)  NULL,
        fecha_anulacion               DATETIME      NULL,
        codigo_motivo_anulacion       INT           NULL,
        fecha_registro                DATETIME      NOT NULL CONSTRAINT NOTA_DF_REGISTRO DEFAULT (GETDATE()),
        CONSTRAINT NOTA_FACTURA_FK  FOREIGN KEY (FACTURA_id_factura) REFERENCES FACTURACION.FACTURA (id_factura),
        CONSTRAINT NOTA_SUCURSAL_FK FOREIGN KEY (TIENDA_id_tienda)   REFERENCES FACTURACION.SUCURSAL (TIENDA_id_tienda),
        CONSTRAINT NOTA_UQ_CUF UNIQUE (cuf),
        CONSTRAINT NOTA_UQ_NUMERO UNIQUE (TIENDA_id_tienda, numero_nota)
    );
GO

IF OBJECT_ID('FACTURACION.NOTA_DETALLE', 'U') IS NULL
    CREATE TABLE FACTURACION.NOTA_DETALLE (
        id_nota_detalle              INT IDENTITY(1,1) NOT NULL CONSTRAINT NOTA_DETALLE_PK PRIMARY KEY,
        NOTA_id_nota                 INT           NOT NULL,
        FACTURA_DETALLE_id           INT           NOT NULL,
        codigo_detalle_transaccion   TINYINT       NOT NULL,   -- 1 original, 2 devuelto
        actividad_economica          VARCHAR(10)   NOT NULL,
        codigo_producto_sin          INT           NOT NULL,
        codigo_producto              VARCHAR(50)   NOT NULL,
        descripcion                  VARCHAR(500)  NOT NULL,
        cantidad                     DECIMAL(17,2) NOT NULL,
        unidad_medida                INT           NOT NULL,
        precio_unitario              DECIMAL(17,2) NOT NULL,
        monto_descuento              DECIMAL(17,2) NOT NULL,
        sub_total                    DECIMAL(17,2) NOT NULL,
        CONSTRAINT NOTA_DETALLE_NOTA_FK FOREIGN KEY (NOTA_id_nota) REFERENCES FACTURACION.NOTA_CREDITO_DEBITO (id_nota),
        CONSTRAINT NOTA_DETALLE_FD_FK   FOREIGN KEY (FACTURA_DETALLE_id) REFERENCES FACTURACION.FACTURA_DETALLE (id_factura_detalle)
    );
GO

IF TYPE_ID('FACTURACION.DetalleNotaTipo') IS NULL
    CREATE TYPE FACTURACION.DetalleNotaTipo AS TABLE (
        factura_detalle_id          INT           NOT NULL,
        codigo_detalle_transaccion  TINYINT       NOT NULL,
        actividad_economica         VARCHAR(10)   NOT NULL,
        codigo_producto_sin         INT           NOT NULL,
        codigo_producto             VARCHAR(50)   NOT NULL,
        descripcion                 VARCHAR(500)  NOT NULL,
        cantidad                    DECIMAL(17,2) NOT NULL,
        unidad_medida               INT           NOT NULL,
        precio_unitario             DECIMAL(17,2) NOT NULL,
        monto_descuento             DECIMAL(17,2) NOT NULL,
        sub_total                   DECIMAL(17,2) NOT NULL,
        id_producto                 INT           NULL      -- para reingresar stock
    );
GO

/* =====================================================================================
   PROCEDIMIENTOS ALMACENADOS
   ===================================================================================== */

-- ---------- Configuración ----------
CREATE OR ALTER PROCEDURE FACTURACION.ObtenerConfiguracion
AS
BEGIN
    SET NOCOUNT ON;
    SELECT nit, razon_social, codigo_sistema, codigo_ambiente, codigo_modalidad, actividad_economica,
           modo_simulado, token_delegado, fecha_actualizacion, simular_sin_conexion
    FROM FACTURACION.CONFIGURACION
    WHERE id_configuracion = 1;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.GuardarConfiguracion
    @Nit                 BIGINT,
    @RazonSocial         VARCHAR(200),
    @CodigoSistema       VARCHAR(50),
    @CodigoAmbiente      TINYINT,
    @CodigoModalidad     TINYINT,
    @ActividadEconomica  VARCHAR(10),
    @ModoSimulado        BIT,
    @TokenDelegado       VARCHAR(MAX) = NULL,
    @Mensaje             VARCHAR(500) OUTPUT,
    @Resultado           BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Resultado = 0;

    IF EXISTS (SELECT 1 FROM FACTURACION.CONFIGURACION WHERE id_configuracion = 1)
        UPDATE FACTURACION.CONFIGURACION
        SET nit = @Nit, razon_social = @RazonSocial, codigo_sistema = @CodigoSistema,
            codigo_ambiente = @CodigoAmbiente, codigo_modalidad = @CodigoModalidad,
            actividad_economica = @ActividadEconomica, modo_simulado = @ModoSimulado,
            token_delegado = ISNULL(@TokenDelegado, token_delegado), fecha_actualizacion = GETDATE()
        WHERE id_configuracion = 1;
    ELSE
        INSERT INTO FACTURACION.CONFIGURACION (id_configuracion, nit, razon_social, codigo_sistema, codigo_ambiente,
                                               codigo_modalidad, actividad_economica, modo_simulado, token_delegado)
        VALUES (1, @Nit, @RazonSocial, @CodigoSistema, @CodigoAmbiente, @CodigoModalidad,
                @ActividadEconomica, @ModoSimulado, @TokenDelegado);

    SET @Resultado = 1;
    SET @Mensaje = 'Configuración guardada correctamente.';
END
GO

-- ---------- Sucursales, CUIS y CUFD ----------
CREATE OR ALTER PROCEDURE FACTURACION.ListarSucursales
AS
BEGIN
    SET NOCOUNT ON;
    SELECT T.id_tienda, T.nombre AS nombre_tienda, T.dirrecion AS direccion_tienda,
           CAST(CASE WHEN S.TIENDA_id_tienda IS NULL THEN 0 ELSE 1 END AS BIT) AS configurada,
           ISNULL(S.codigo_sucursal, 0) AS codigo_sucursal, ISNULL(S.codigo_punto_venta, 0) AS codigo_punto_venta,
           S.municipio, S.telefono,
           CI.id_cuis, CI.codigo AS cuis_codigo, CI.fecha_solicitud AS cuis_fecha_solicitud, CI.fecha_vigencia AS cuis_fecha_vigencia,
           CF.id_cufd, CF.codigo AS cufd_codigo, CF.codigo_control AS cufd_codigo_control, CF.direccion AS cufd_direccion,
           CF.fecha_solicitud AS cufd_fecha_solicitud, CF.fecha_vigencia AS cufd_fecha_vigencia
    FROM INVENTARIO.TIENDA T
    LEFT JOIN FACTURACION.SUCURSAL S ON S.TIENDA_id_tienda = T.id_tienda
    OUTER APPLY (SELECT TOP 1 * FROM FACTURACION.CUIS C
                 WHERE C.TIENDA_id_tienda = T.id_tienda AND C.activo = 1 ORDER BY C.id_cuis DESC) CI
    OUTER APPLY (SELECT TOP 1 * FROM FACTURACION.CUFD C
                 WHERE C.TIENDA_id_tienda = T.id_tienda AND C.activo = 1 ORDER BY C.id_cufd DESC) CF
    WHERE T.estado = 1
    ORDER BY T.id_tienda;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.GuardarSucursal
    @IdTienda          INT,
    @CodigoSucursal    INT,
    @CodigoPuntoVenta  INT,
    @Municipio         VARCHAR(25),
    @Telefono          VARCHAR(25) = NULL,
    @Mensaje           VARCHAR(500) OUTPUT,
    @Resultado         BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Resultado = 0;

    IF EXISTS (SELECT 1 FROM FACTURACION.SUCURSAL
               WHERE codigo_sucursal = @CodigoSucursal AND codigo_punto_venta = @CodigoPuntoVenta
                 AND TIENDA_id_tienda <> @IdTienda)
    BEGIN
        SET @Mensaje = 'Ya existe otra tienda con el mismo código de sucursal y punto de venta.';
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM FACTURACION.SUCURSAL WHERE TIENDA_id_tienda = @IdTienda)
    BEGIN
        -- Si cambian los códigos, el CUIS y CUFD dejan de corresponder a la sucursal.
        IF EXISTS (SELECT 1 FROM FACTURACION.SUCURSAL
                   WHERE TIENDA_id_tienda = @IdTienda
                     AND (codigo_sucursal <> @CodigoSucursal OR codigo_punto_venta <> @CodigoPuntoVenta))
        BEGIN
            UPDATE FACTURACION.CUIS SET activo = 0 WHERE TIENDA_id_tienda = @IdTienda;
            UPDATE FACTURACION.CUFD SET activo = 0 WHERE TIENDA_id_tienda = @IdTienda;
        END

        UPDATE FACTURACION.SUCURSAL
        SET codigo_sucursal = @CodigoSucursal, codigo_punto_venta = @CodigoPuntoVenta,
            municipio = @Municipio, telefono = @Telefono
        WHERE TIENDA_id_tienda = @IdTienda;
    END
    ELSE
    BEGIN
        INSERT INTO FACTURACION.SUCURSAL (TIENDA_id_tienda, codigo_sucursal, codigo_punto_venta, municipio, telefono)
        VALUES (@IdTienda, @CodigoSucursal, @CodigoPuntoVenta, @Municipio, @Telefono);

        INSERT INTO FACTURACION.NUMERACION (TIENDA_id_tienda, ultimo_numero) VALUES (@IdTienda, 0);
    END

    SET @Resultado = 1;
    SET @Mensaje = 'Sucursal guardada correctamente.';
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.RegistrarCuis
    @IdTienda       INT,
    @Codigo         VARCHAR(100),
    @FechaVigencia  DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
        UPDATE FACTURACION.CUIS SET activo = 0 WHERE TIENDA_id_tienda = @IdTienda;
        -- Un CUIS nuevo invalida el CUFD anterior (el anexo exige pedir un CUFD nuevo).
        UPDATE FACTURACION.CUFD SET activo = 0 WHERE TIENDA_id_tienda = @IdTienda;
        INSERT INTO FACTURACION.CUIS (TIENDA_id_tienda, codigo, fecha_vigencia) VALUES (@IdTienda, @Codigo, @FechaVigencia);
    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.RegistrarCufd
    @IdTienda       INT,
    @Codigo         VARCHAR(100),
    @CodigoControl  VARCHAR(50),
    @Direccion      VARCHAR(500),
    @FechaVigencia  DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
        UPDATE FACTURACION.CUFD SET activo = 0 WHERE TIENDA_id_tienda = @IdTienda;
        INSERT INTO FACTURACION.CUFD (TIENDA_id_tienda, codigo, codigo_control, direccion, fecha_vigencia)
        VALUES (@IdTienda, @Codigo, @CodigoControl, @Direccion, @FechaVigencia);
    COMMIT TRANSACTION;
END
GO

-- ---------- Catálogos ----------
CREATE OR ALTER PROCEDURE FACTURACION.ReemplazarCatalogo
    @Tipo       VARCHAR(40),
    @Registros  FACTURACION.CatalogoTipo READONLY
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
        DELETE FROM FACTURACION.CATALOGO WHERE tipo = @Tipo;
        INSERT INTO FACTURACION.CATALOGO (tipo, codigo, descripcion, codigo_actividad)
        SELECT @Tipo, codigo, descripcion, codigo_actividad FROM @Registros;
    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.ListarCatalogo
    @Tipo  VARCHAR(40)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT tipo, codigo, descripcion, codigo_actividad, fecha_sincronizacion
    FROM FACTURACION.CATALOGO
    WHERE tipo = @Tipo
    ORDER BY TRY_CAST(codigo AS BIGINT), codigo;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.ResumenCatalogos
AS
BEGIN
    SET NOCOUNT ON;
    SELECT tipo, COUNT(*) AS cantidad, MAX(fecha_sincronizacion) AS ultima_sincronizacion
    FROM FACTURACION.CATALOGO
    GROUP BY tipo;
END
GO

-- ---------- Homologación ----------
CREATE OR ALTER PROCEDURE FACTURACION.ListarHomologacion
    @Tipo  VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    IF @Tipo = 'PRODUCTO'
        SELECT 'PRODUCTO' AS tipo, P.id_producto AS id_registro, P.nombre AS descripcion,
               CAST(H.codigo_producto_sin AS VARCHAR(20)) AS codigo_sin, C.descripcion AS descripcion_sin
        FROM INVENTARIO.PRODUCTO P
        LEFT JOIN FACTURACION.HOMOLOGACION_PRODUCTO H ON H.PRODUCTO_id_producto = P.id_producto
        LEFT JOIN FACTURACION.CATALOGO C ON C.tipo = 'PRODUCTO_SERVICIO' AND C.codigo = CAST(H.codigo_producto_sin AS VARCHAR(20))
        WHERE P.estado = 1
        ORDER BY P.nombre;
    ELSE IF @Tipo = 'SERVICIO'
        SELECT 'SERVICIO' AS tipo, S.id_servicio AS id_registro, S.nombre AS descripcion,
               CAST(H.codigo_producto_sin AS VARCHAR(20)) AS codigo_sin, C.descripcion AS descripcion_sin
        FROM TRANSACCIONES.SERVICIO S
        LEFT JOIN FACTURACION.HOMOLOGACION_SERVICIO H ON H.SERVICIO_id_servicio = S.id_servicio
        LEFT JOIN FACTURACION.CATALOGO C ON C.tipo = 'PRODUCTO_SERVICIO' AND C.codigo = CAST(H.codigo_producto_sin AS VARCHAR(20))
        WHERE S.estado = 1
        ORDER BY S.nombre;
    ELSE IF @Tipo = 'UNIDAD_MEDIDA'
        SELECT 'UNIDAD_MEDIDA' AS tipo, U.id_unidad_medida AS id_registro, U.descripcion,
               CAST(H.codigo_unidad_medida AS VARCHAR(20)) AS codigo_sin, C.descripcion AS descripcion_sin
        FROM INVENTARIO.UNIDAD_MEDIDA U
        LEFT JOIN FACTURACION.HOMOLOGACION_UNIDAD_MEDIDA H ON H.UNIDAD_MEDIDA_id_unidad_medida = U.id_unidad_medida
        LEFT JOIN FACTURACION.CATALOGO C ON C.tipo = 'UNIDAD_MEDIDA' AND C.codigo = CAST(H.codigo_unidad_medida AS VARCHAR(20))
        WHERE U.estado = 1
        ORDER BY U.descripcion;
    ELSE IF @Tipo = 'TIPO_PAGO'
        SELECT 'TIPO_PAGO' AS tipo, T.id_tipo_pago AS id_registro, T.descripcion,
               CAST(H.codigo_metodo_pago AS VARCHAR(20)) AS codigo_sin, C.descripcion AS descripcion_sin
        FROM TRANSACCIONES.TIPO_PAGO T
        LEFT JOIN FACTURACION.HOMOLOGACION_TIPO_PAGO H ON H.TIPO_PAGO_id_tipo_pago = T.id_tipo_pago
        LEFT JOIN FACTURACION.CATALOGO C ON C.tipo = 'METODO_PAGO' AND C.codigo = CAST(H.codigo_metodo_pago AS VARCHAR(20))
        WHERE T.estado = 1
        ORDER BY T.descripcion;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.GuardarHomologacion
    @Tipo        VARCHAR(20),
    @IdRegistro  INT,
    @CodigoSin   INT = NULL,   -- NULL elimina la homologación
    @Mensaje     VARCHAR(500) OUTPUT,
    @Resultado   BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Resultado = 0;

    DECLARE @TipoCatalogo VARCHAR(40) =
        CASE @Tipo WHEN 'PRODUCTO' THEN 'PRODUCTO_SERVICIO' WHEN 'SERVICIO' THEN 'PRODUCTO_SERVICIO'
                   WHEN 'UNIDAD_MEDIDA' THEN 'UNIDAD_MEDIDA' WHEN 'TIPO_PAGO' THEN 'METODO_PAGO' END;

    IF @TipoCatalogo IS NULL
    BEGIN
        SET @Mensaje = 'Tipo de homologación no válido.';
        RETURN;
    END

    IF @CodigoSin IS NOT NULL AND NOT EXISTS (SELECT 1 FROM FACTURACION.CATALOGO
                                              WHERE tipo = @TipoCatalogo AND codigo = CAST(@CodigoSin AS VARCHAR(20)))
    BEGIN
        SET @Mensaje = 'El código ' + CAST(@CodigoSin AS VARCHAR(20)) + ' no existe en el catálogo del SIN. Sincronice los catálogos.';
        RETURN;
    END

    IF @Tipo = 'PRODUCTO'
    BEGIN
        DELETE FROM FACTURACION.HOMOLOGACION_PRODUCTO WHERE PRODUCTO_id_producto = @IdRegistro;
        IF @CodigoSin IS NOT NULL
            INSERT INTO FACTURACION.HOMOLOGACION_PRODUCTO (PRODUCTO_id_producto, codigo_producto_sin) VALUES (@IdRegistro, @CodigoSin);
    END
    ELSE IF @Tipo = 'SERVICIO'
    BEGIN
        DELETE FROM FACTURACION.HOMOLOGACION_SERVICIO WHERE SERVICIO_id_servicio = @IdRegistro;
        IF @CodigoSin IS NOT NULL
            INSERT INTO FACTURACION.HOMOLOGACION_SERVICIO (SERVICIO_id_servicio, codigo_producto_sin) VALUES (@IdRegistro, @CodigoSin);
    END
    ELSE IF @Tipo = 'UNIDAD_MEDIDA'
    BEGIN
        DELETE FROM FACTURACION.HOMOLOGACION_UNIDAD_MEDIDA WHERE UNIDAD_MEDIDA_id_unidad_medida = @IdRegistro;
        IF @CodigoSin IS NOT NULL
            INSERT INTO FACTURACION.HOMOLOGACION_UNIDAD_MEDIDA (UNIDAD_MEDIDA_id_unidad_medida, codigo_unidad_medida) VALUES (@IdRegistro, @CodigoSin);
    END
    ELSE IF @Tipo = 'TIPO_PAGO'
    BEGIN
        DELETE FROM FACTURACION.HOMOLOGACION_TIPO_PAGO WHERE TIPO_PAGO_id_tipo_pago = @IdRegistro;
        IF @CodigoSin IS NOT NULL
            INSERT INTO FACTURACION.HOMOLOGACION_TIPO_PAGO (TIPO_PAGO_id_tipo_pago, codigo_metodo_pago) VALUES (@IdRegistro, @CodigoSin);
    END

    SET @Resultado = 1;
    SET @Mensaje = 'Homologación guardada correctamente.';
END
GO

-- ---------- Datos fiscales del cliente ----------
CREATE OR ALTER PROCEDURE FACTURACION.ObtenerDatosFiscalesCliente
    @IdCliente  INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT C.id_cliente,
           F.codigo_tipo_documento, F.numero_documento, F.complemento,
           ISNULL(F.razon_social, LTRIM(RTRIM(C.nombres + ' ' + ISNULL(C.apellidos, '')))) AS razon_social,
           ISNULL(F.correo, C.correo) AS correo
    FROM COMERCIAL.CLIENTE C
    LEFT JOIN FACTURACION.CLIENTE_FISCAL F ON F.CLIENTE_id_cliente = C.id_cliente
    WHERE C.id_cliente = @IdCliente;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.GuardarDatosFiscalesCliente
    @IdCliente            INT,
    @CodigoTipoDocumento  INT,
    @NumeroDocumento      VARCHAR(20),
    @Complemento          VARCHAR(5) = NULL,
    @RazonSocial          VARCHAR(500),
    @Correo               VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM FACTURACION.CLIENTE_FISCAL WHERE CLIENTE_id_cliente = @IdCliente)
        UPDATE FACTURACION.CLIENTE_FISCAL
        SET codigo_tipo_documento = @CodigoTipoDocumento, numero_documento = @NumeroDocumento,
            complemento = @Complemento, razon_social = @RazonSocial, correo = @Correo
        WHERE CLIENTE_id_cliente = @IdCliente;
    ELSE
        INSERT INTO FACTURACION.CLIENTE_FISCAL (CLIENTE_id_cliente, codigo_tipo_documento, numero_documento,
                                                complemento, razon_social, correo)
        VALUES (@IdCliente, @CodigoTipoDocumento, @NumeroDocumento, @Complemento, @RazonSocial, @Correo);
END
GO

-- ---------- Datos de la venta para armar la factura ----------
CREATE OR ALTER PROCEDURE FACTURACION.ObtenerVentaParaFacturar
    @IdVenta  INT
AS
BEGIN
    SET NOCOUNT ON;

    -- 1) Cabecera
    SELECT V.id_venta, V.fecha, V.monto_total, V.CLIENTE_id_cliente AS id_cliente,
           V.TIPO_PAGO_id_tipo_pago AS id_tipo_pago, TP.descripcion AS tipo_pago,
           HTP.codigo_metodo_pago,
           (SELECT TOP 1 F.estado FROM FACTURACION.FACTURA F
            WHERE F.VENTA_id_venta = V.id_venta AND F.estado IN ('VALIDADA', 'PENDIENTE')) AS estado_factura_vigente
    FROM TRANSACCIONES.VENTA V
    INNER JOIN TRANSACCIONES.TIPO_PAGO TP ON TP.id_tipo_pago = V.TIPO_PAGO_id_tipo_pago
    LEFT JOIN FACTURACION.HOMOLOGACION_TIPO_PAGO HTP ON HTP.TIPO_PAGO_id_tipo_pago = V.TIPO_PAGO_id_tipo_pago
    WHERE V.id_venta = @IdVenta;

    -- 2) Detalle con los códigos homologados
    SELECT DV.id_detalle_venta, DV.tipo_item, DV.cantidad, DV.precio, DV.sub_total,
           DV.PRODUCTO_id_producto AS id_producto, DV.SERVICIO_id_servicio AS id_servicio,
           CASE WHEN DV.tipo_item = 'PRODUCTO' THEN P.nombre ELSE S.nombre END AS descripcion,
           CASE WHEN DV.tipo_item = 'PRODUCTO' THEN HP.codigo_producto_sin ELSE HS.codigo_producto_sin END AS codigo_producto_sin,
           HU.codigo_unidad_medida
    FROM TRANSACCIONES.DETALLE_VENTA DV
    LEFT JOIN INVENTARIO.PRODUCTO P ON P.id_producto = DV.PRODUCTO_id_producto
    LEFT JOIN TRANSACCIONES.SERVICIO S ON S.id_servicio = DV.SERVICIO_id_servicio
    LEFT JOIN FACTURACION.HOMOLOGACION_PRODUCTO HP ON HP.PRODUCTO_id_producto = DV.PRODUCTO_id_producto
    LEFT JOIN FACTURACION.HOMOLOGACION_SERVICIO HS ON HS.SERVICIO_id_servicio = DV.SERVICIO_id_servicio
    LEFT JOIN FACTURACION.HOMOLOGACION_UNIDAD_MEDIDA HU ON HU.UNIDAD_MEDIDA_id_unidad_medida = P.UNIDAD_MEDIDA_id_unidad_medida
    WHERE DV.VENTA_id_venta = @IdVenta
    ORDER BY DV.id_detalle_venta;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.ListarVentasPendientes
    @FechaInicio  DATE = NULL,
    @FechaFin     DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT V.id_venta, V.fecha, V.monto_total, C.id_cliente,
           LTRIM(RTRIM(C.nombres + ' ' + ISNULL(C.apellidos, ''))) AS cliente, TP.descripcion AS tipo_pago
    FROM TRANSACCIONES.VENTA V
    INNER JOIN COMERCIAL.CLIENTE C ON C.id_cliente = V.CLIENTE_id_cliente
    INNER JOIN TRANSACCIONES.TIPO_PAGO TP ON TP.id_tipo_pago = V.TIPO_PAGO_id_tipo_pago
    WHERE NOT EXISTS (SELECT 1 FROM FACTURACION.FACTURA F
                      WHERE F.VENTA_id_venta = V.id_venta AND F.estado IN ('VALIDADA', 'PENDIENTE'))
      AND (@FechaInicio IS NULL OR V.fecha >= @FechaInicio)
      AND (@FechaFin IS NULL OR V.fecha <= @FechaFin)
    ORDER BY V.id_venta DESC;
END
GO

-- ---------- Emisión ----------
CREATE OR ALTER PROCEDURE FACTURACION.ObtenerSiguienteNumeroFactura
    @IdTienda  INT,
    @Numero    BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    -- UPDATE atómico: dos ventas simultáneas nunca obtienen el mismo número.
    UPDATE FACTURACION.NUMERACION WITH (UPDLOCK, ROWLOCK)
    SET @Numero = ultimo_numero = ultimo_numero + 1
    WHERE TIENDA_id_tienda = @IdTienda;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO FACTURACION.NUMERACION (TIENDA_id_tienda, ultimo_numero) VALUES (@IdTienda, 1);
        SET @Numero = 1;
    END
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.RegistrarFactura
    @IdVenta               INT,
    @IdTienda              INT,
    @NitEmisor             BIGINT,
    @RazonSocialEmisor     VARCHAR(200),
    @Municipio             VARCHAR(25),
    @Telefono              VARCHAR(25),
    @Direccion             VARCHAR(500),
    @CodigoSucursal        INT,
    @CodigoPuntoVenta      INT,
    @NumeroFactura         BIGINT,
    @Cuf                   VARCHAR(100),
    @Cufd                  VARCHAR(100),
    @FechaEmision          DATETIME2(3),
    @CodigoModalidad       TINYINT,
    @TipoEmision           TINYINT,
    @TipoFactura           TINYINT,
    @CodigoDocumentoSector INT,
    @NombreRazonSocial     VARCHAR(500),
    @CodigoTipoDocumento   INT,
    @NumeroDocumento       VARCHAR(20),
    @Complemento           VARCHAR(5),
    @CodigoCliente         VARCHAR(100),
    @CorreoCliente         VARCHAR(100),
    @CodigoMetodoPago      INT,
    @NumeroTarjeta         VARCHAR(16),
    @MontoTotal            DECIMAL(17,2),
    @MontoTotalSujetoIva   DECIMAL(17,2),
    @DescuentoAdicional    DECIMAL(17,2),
    @CodigoMoneda          INT,
    @TipoCambio            DECIMAL(17,2),
    @MontoTotalMoneda      DECIMAL(17,2),
    @CodigoExcepcion       TINYINT,
    @Leyenda               VARCHAR(200),
    @Usuario               VARCHAR(100),
    @ArchivoXml            NVARCHAR(MAX),
    @HashArchivo           VARCHAR(64),
    @CodigoRecepcion       VARCHAR(100),
    @Estado                VARCHAR(20),
    @CodigoEstado          INT,
    @Mensajes              VARCHAR(MAX),
    @Detalle               FACTURACION.DetalleFacturaTipo READONLY,
    @IdEvento              INT = NULL,
    @Mensaje               VARCHAR(500) OUTPUT,
    @Resultado             INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Resultado = 0;

    BEGIN TRANSACTION;
    BEGIN TRY
        INSERT INTO FACTURACION.FACTURA (
            VENTA_id_venta, TIENDA_id_tienda, nit_emisor, razon_social_emisor, municipio, telefono, direccion,
            codigo_sucursal, codigo_punto_venta, numero_factura, cuf, cufd, fecha_emision, codigo_modalidad,
            tipo_emision, tipo_factura, codigo_documento_sector, nombre_razon_social, codigo_tipo_documento,
            numero_documento, complemento, codigo_cliente, correo_cliente, codigo_metodo_pago, numero_tarjeta,
            monto_total, monto_total_sujeto_iva, descuento_adicional, codigo_moneda, tipo_cambio, monto_total_moneda,
            codigo_excepcion, leyenda, usuario, archivo_xml, hash_archivo, codigo_recepcion, estado, codigo_estado, mensajes,
            EVENTO_id_evento)
        VALUES (
            @IdVenta, @IdTienda, @NitEmisor, @RazonSocialEmisor, @Municipio, @Telefono, @Direccion,
            @CodigoSucursal, @CodigoPuntoVenta, @NumeroFactura, @Cuf, @Cufd, @FechaEmision, @CodigoModalidad,
            @TipoEmision, @TipoFactura, @CodigoDocumentoSector, @NombreRazonSocial, @CodigoTipoDocumento,
            @NumeroDocumento, @Complemento, @CodigoCliente, @CorreoCliente, @CodigoMetodoPago, @NumeroTarjeta,
            @MontoTotal, @MontoTotalSujetoIva, @DescuentoAdicional, @CodigoMoneda, @TipoCambio, @MontoTotalMoneda,
            @CodigoExcepcion, @Leyenda, @Usuario, @ArchivoXml, @HashArchivo, @CodigoRecepcion, @Estado, @CodigoEstado, @Mensajes,
            @IdEvento);

        DECLARE @IdFactura INT = SCOPE_IDENTITY();

        INSERT INTO FACTURACION.FACTURA_DETALLE (FACTURA_id_factura, actividad_economica, codigo_producto_sin, codigo_producto,
                                                 descripcion, cantidad, unidad_medida, precio_unitario, monto_descuento,
                                                 sub_total, numero_serie)
        SELECT @IdFactura, actividad_economica, codigo_producto_sin, codigo_producto, descripcion, cantidad,
               unidad_medida, precio_unitario, monto_descuento, sub_total, numero_serie
        FROM @Detalle;

        COMMIT TRANSACTION;
        SET @Resultado = @IdFactura;
        SET @Mensaje = 'Factura registrada correctamente.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        SET @Resultado = 0;
        SET @Mensaje = 'Error al registrar la factura: ' + ERROR_MESSAGE();
    END CATCH
END
GO

-- ---------- Consultas ----------
CREATE OR ALTER PROCEDURE FACTURACION.ListarFacturas
    @FechaInicio  DATE = NULL,
    @FechaFin     DATE = NULL,
    @Estado       VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT F.id_factura, F.VENTA_id_venta AS id_venta, F.TIENDA_id_tienda AS id_tienda, F.numero_factura, F.cuf,
           F.fecha_emision, F.nombre_razon_social, F.codigo_tipo_documento, F.numero_documento, F.complemento,
           F.monto_total, F.estado, F.codigo_estado, F.codigo_recepcion, F.mensajes, F.tipo_emision,
           F.fecha_anulacion, F.codigo_motivo_anulacion, MA.descripcion AS motivo_anulacion, F.codigo_sucursal,
           F.codigo_punto_venta, F.correo_cliente, F.EVENTO_id_evento AS id_evento
    FROM FACTURACION.FACTURA F
    LEFT JOIN FACTURACION.CATALOGO MA ON MA.tipo = 'MOTIVO_ANULACION' AND MA.codigo = CAST(F.codigo_motivo_anulacion AS VARCHAR(20))
    WHERE (@FechaInicio IS NULL OR CAST(F.fecha_emision AS DATE) >= @FechaInicio)
      AND (@FechaFin IS NULL OR CAST(F.fecha_emision AS DATE) <= @FechaFin)
      AND (@Estado IS NULL OR @Estado = '' OR F.estado = @Estado)
    ORDER BY F.id_factura DESC;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.ObtenerFactura
    @IdFactura  INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT F.*, MA.descripcion AS motivo_anulacion
    FROM FACTURACION.FACTURA F
    LEFT JOIN FACTURACION.CATALOGO MA ON MA.tipo = 'MOTIVO_ANULACION' AND MA.codigo = CAST(F.codigo_motivo_anulacion AS VARCHAR(20))
    WHERE F.id_factura = @IdFactura;

    SELECT D.*, U.descripcion AS descripcion_unidad_medida
    FROM FACTURACION.FACTURA_DETALLE D
    LEFT JOIN FACTURACION.CATALOGO U ON U.tipo = 'UNIDAD_MEDIDA' AND U.codigo = CAST(D.unidad_medida AS VARCHAR(20))
    WHERE D.FACTURA_id_factura = @IdFactura
    ORDER BY D.id_factura_detalle;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.RegistrarAnulacion
    @IdFactura     INT,
    @CodigoMotivo  INT,
    @CodigoEstado  INT,
    @Mensajes      VARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE FACTURACION.FACTURA
    SET estado = 'ANULADA', codigo_estado = @CodigoEstado, codigo_motivo_anulacion = @CodigoMotivo,
        fecha_anulacion = GETDATE(), mensajes = ISNULL(@Mensajes, mensajes)
    WHERE id_factura = @IdFactura AND estado = 'VALIDADA';

    SELECT @@ROWCOUNT AS filas_afectadas;
END
GO

-- ---------- Contingencia ----------
CREATE OR ALTER PROCEDURE FACTURACION.GuardarSimulacionSinConexion
    @SimularSinConexion  BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE FACTURACION.CONFIGURACION SET simular_sin_conexion = @SimularSinConexion WHERE id_configuracion = 1;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.IniciarEvento
    @IdTienda             INT,
    @CodigoEvento         INT,
    @Descripcion          VARCHAR(500),
    @FechaInicio          DATETIME2(3),
    @CufdEvento           VARCHAR(100),
    @CodigoControlEvento  VARCHAR(50),
    @Automatico           BIT,
    @Mensaje              VARCHAR(500) OUTPUT,
    @Resultado            INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Resultado = 0;

    IF EXISTS (SELECT 1 FROM FACTURACION.EVENTO_SIGNIFICATIVO WHERE TIENDA_id_tienda = @IdTienda AND estado = 'ABIERTO')
    BEGIN
        SET @Mensaje = 'La tienda ya tiene una contingencia abierta.';
        SELECT @Resultado = id_evento FROM FACTURACION.EVENTO_SIGNIFICATIVO WHERE TIENDA_id_tienda = @IdTienda AND estado = 'ABIERTO';
        SET @Resultado = -@Resultado;   -- negativo: ya existía
        RETURN;
    END

    INSERT INTO FACTURACION.EVENTO_SIGNIFICATIVO (TIENDA_id_tienda, codigo_evento, descripcion, fecha_inicio, cufd_evento,
                                                  codigo_control_evento, automatico, estado)
    VALUES (@IdTienda, @CodigoEvento, @Descripcion, @FechaInicio, @CufdEvento, @CodigoControlEvento, @Automatico, 'ABIERTO');

    SET @Resultado = SCOPE_IDENTITY();
    SET @Mensaje = 'Contingencia iniciada.';
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.ListarEventos
    @IdTienda  INT = NULL,
    @IdEvento  INT = NULL,
    @SoloAbiertos BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT E.id_evento, E.TIENDA_id_tienda AS id_tienda, T.nombre AS nombre_tienda, E.codigo_evento,
           C.descripcion AS descripcion_evento, E.descripcion, E.fecha_inicio, E.fecha_fin, E.cufd_evento,
           E.codigo_control_evento, E.automatico, E.estado, E.codigo_recepcion_evento, E.mensajes,
           (SELECT COUNT(*) FROM FACTURACION.FACTURA F WHERE F.EVENTO_id_evento = E.id_evento) AS cantidad_facturas,
           (SELECT COUNT(*) FROM FACTURACION.FACTURA F WHERE F.EVENTO_id_evento = E.id_evento AND F.estado = 'PENDIENTE') AS facturas_pendientes
    FROM FACTURACION.EVENTO_SIGNIFICATIVO E
    INNER JOIN INVENTARIO.TIENDA T ON T.id_tienda = E.TIENDA_id_tienda
    LEFT JOIN FACTURACION.CATALOGO C ON C.tipo = 'EVENTO_SIGNIFICATIVO' AND C.codigo = CAST(E.codigo_evento AS VARCHAR(20))
    WHERE (@IdTienda IS NULL OR E.TIENDA_id_tienda = @IdTienda)
      AND (@IdEvento IS NULL OR E.id_evento = @IdEvento)
      AND (@SoloAbiertos = 0 OR E.estado = 'ABIERTO')
    ORDER BY E.id_evento DESC;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.ActualizarEvento
    @IdEvento               INT,
    @Estado                 VARCHAR(20),
    @FechaFin               DATETIME2(3) = NULL,
    @CodigoRecepcionEvento  VARCHAR(100) = NULL,
    @Mensajes               VARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE FACTURACION.EVENTO_SIGNIFICATIVO
    SET estado = @Estado,
        fecha_fin = ISNULL(@FechaFin, fecha_fin),
        codigo_recepcion_evento = ISNULL(@CodigoRecepcionEvento, codigo_recepcion_evento),
        mensajes = @Mensajes
    WHERE id_evento = @IdEvento;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.ListarFacturasPendientesEvento
    @IdEvento  INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id_factura, numero_factura, archivo_xml
    FROM FACTURACION.FACTURA
    WHERE EVENTO_id_evento = @IdEvento AND estado = 'PENDIENTE'
    ORDER BY numero_factura;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.RegistrarPaquete
    @IdEvento          INT,
    @CantidadFacturas  INT,
    @HashArchivo       VARCHAR(64),
    @CodigoRecepcion   VARCHAR(100),
    @CodigoEstado      INT,
    @Estado            VARCHAR(20),
    @Mensajes          VARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO FACTURACION.PAQUETE (EVENTO_id_evento, cantidad_facturas, hash_archivo, codigo_recepcion, codigo_estado, estado, mensajes)
    VALUES (@IdEvento, @CantidadFacturas, @HashArchivo, @CodigoRecepcion, @CodigoEstado, @Estado, @Mensajes);
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.ActualizarEstadoFactura
    @IdFactura        INT,
    @Estado           VARCHAR(20),
    @CodigoEstado     INT,
    @CodigoRecepcion  VARCHAR(100),
    @Mensajes         VARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE FACTURACION.FACTURA
    SET estado = @Estado, codigo_estado = @CodigoEstado, codigo_recepcion = @CodigoRecepcion, mensajes = @Mensajes
    WHERE id_factura = @IdFactura AND estado = 'PENDIENTE';
END
GO

-- ---------- Notas de crédito-débito ----------
CREATE OR ALTER PROCEDURE FACTURACION.ObtenerFacturaParaNota
    @IdFactura  INT
AS
BEGIN
    SET NOCOUNT ON;
    -- Detalle de la factura con lo ya devuelto en notas vigentes (no anuladas).
    SELECT D.id_factura_detalle, D.actividad_economica, D.codigo_producto_sin, D.codigo_producto, D.descripcion,
           D.cantidad, D.unidad_medida, D.precio_unitario, D.monto_descuento, D.sub_total,
           ISNULL((SELECT SUM(ND.cantidad)
                   FROM FACTURACION.NOTA_DETALLE ND
                   INNER JOIN FACTURACION.NOTA_CREDITO_DEBITO N ON N.id_nota = ND.NOTA_id_nota
                   WHERE ND.FACTURA_DETALLE_id = D.id_factura_detalle AND ND.codigo_detalle_transaccion = 2
                     AND N.estado = 'VALIDADA'), 0) AS cantidad_devuelta
    FROM FACTURACION.FACTURA_DETALLE D
    WHERE D.FACTURA_id_factura = @IdFactura
    ORDER BY D.id_factura_detalle;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.ObtenerSiguienteNumeroNota
    @IdTienda  INT,
    @Numero    BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE FACTURACION.NUMERACION WITH (UPDLOCK, ROWLOCK)
    SET @Numero = ultimo_numero_nota = ultimo_numero_nota + 1
    WHERE TIENDA_id_tienda = @IdTienda;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO FACTURACION.NUMERACION (TIENDA_id_tienda, ultimo_numero, ultimo_numero_nota) VALUES (@IdTienda, 0, 1);
        SET @Numero = 1;
    END
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.RegistrarNota
    @IdFactura                  INT,
    @IdTienda                   INT,
    @NumeroNota                 BIGINT,
    @Cuf                        VARCHAR(100),
    @Cufd                       VARCHAR(100),
    @FechaEmision               DATETIME2(3),
    @CodigoSucursal             INT,
    @CodigoPuntoVenta           INT,
    @Direccion                  VARCHAR(500),
    @Municipio                  VARCHAR(25),
    @Telefono                   VARCHAR(25),
    @MontoTotalOriginal         DECIMAL(17,2),
    @MontoTotalDevuelto         DECIMAL(17,2),
    @MontoEfectivoCreditoDebito DECIMAL(17,2),
    @CodigoExcepcion            TINYINT,
    @Leyenda                    VARCHAR(200),
    @Usuario                    VARCHAR(100),
    @Motivo                     VARCHAR(500),
    @ReingresoStock             BIT,
    @ArchivoXml                 NVARCHAR(MAX),
    @HashArchivo                VARCHAR(64),
    @CodigoRecepcion            VARCHAR(100),
    @Estado                     VARCHAR(20),
    @CodigoEstado               INT,
    @Mensajes                   VARCHAR(MAX),
    @Detalle                    FACTURACION.DetalleNotaTipo READONLY,
    @Mensaje                    VARCHAR(500) OUTPUT,
    @Resultado                  INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Resultado = 0;

    BEGIN TRANSACTION;
    BEGIN TRY
        -- Control de concurrencia: lo devuelto (incluida esta nota) no puede superar lo facturado.
        IF @Estado = 'VALIDADA' AND EXISTS (
            SELECT 1
            FROM @Detalle T
            INNER JOIN FACTURACION.FACTURA_DETALLE D ON D.id_factura_detalle = T.factura_detalle_id
            WHERE T.codigo_detalle_transaccion = 2
              AND T.cantidad + ISNULL((SELECT SUM(ND.cantidad)
                                       FROM FACTURACION.NOTA_DETALLE ND WITH (UPDLOCK)
                                       INNER JOIN FACTURACION.NOTA_CREDITO_DEBITO N ON N.id_nota = ND.NOTA_id_nota
                                       WHERE ND.FACTURA_DETALLE_id = D.id_factura_detalle
                                         AND ND.codigo_detalle_transaccion = 2 AND N.estado = 'VALIDADA'), 0) > D.cantidad)
        BEGIN
            ROLLBACK TRANSACTION;
            SET @Mensaje = 'La cantidad devuelta supera la cantidad facturada (otra nota se registró al mismo tiempo).';
            RETURN;
        END

        INSERT INTO FACTURACION.NOTA_CREDITO_DEBITO (
            FACTURA_id_factura, TIENDA_id_tienda, numero_nota, cuf, cufd, fecha_emision, codigo_sucursal, codigo_punto_venta,
            direccion, municipio, telefono, monto_total_original, monto_total_devuelto, monto_efectivo_credito_debito,
            codigo_excepcion, leyenda, usuario, motivo, reingreso_stock, archivo_xml, hash_archivo, codigo_recepcion,
            estado, codigo_estado, mensajes)
        VALUES (
            @IdFactura, @IdTienda, @NumeroNota, @Cuf, @Cufd, @FechaEmision, @CodigoSucursal, @CodigoPuntoVenta,
            @Direccion, @Municipio, @Telefono, @MontoTotalOriginal, @MontoTotalDevuelto, @MontoEfectivoCreditoDebito,
            @CodigoExcepcion, @Leyenda, @Usuario, @Motivo, @ReingresoStock, @ArchivoXml, @HashArchivo, @CodigoRecepcion,
            @Estado, @CodigoEstado, @Mensajes);

        DECLARE @IdNota INT = SCOPE_IDENTITY();

        INSERT INTO FACTURACION.NOTA_DETALLE (NOTA_id_nota, FACTURA_DETALLE_id, codigo_detalle_transaccion, actividad_economica,
                                              codigo_producto_sin, codigo_producto, descripcion, cantidad, unidad_medida,
                                              precio_unitario, monto_descuento, sub_total)
        SELECT @IdNota, factura_detalle_id, codigo_detalle_transaccion, actividad_economica, codigo_producto_sin,
               codigo_producto, descripcion, cantidad, unidad_medida, precio_unitario, monto_descuento, sub_total
        FROM @Detalle;

        -- Reingreso al inventario de los productos devueltos (solo si el SIN validó la nota).
        IF @ReingresoStock = 1 AND @Estado = 'VALIDADA'
            UPDATE S
            SET S.cantidad = S.cantidad + CAST(T.cantidad AS INT), S.fecha_actualizacion = GETDATE()
            FROM INVENTARIO.STOCK S
            INNER JOIN @Detalle T ON T.id_producto = S.PRODUCTO_id_producto
            WHERE T.codigo_detalle_transaccion = 2 AND T.id_producto IS NOT NULL AND S.TIENDA_id_tienda = @IdTienda;

        COMMIT TRANSACTION;
        SET @Resultado = @IdNota;
        SET @Mensaje = 'Nota registrada correctamente.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        SET @Resultado = 0;
        SET @Mensaje = 'Error al registrar la nota: ' + ERROR_MESSAGE();
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.ListarNotas
    @FechaInicio  DATE = NULL,
    @FechaFin     DATE = NULL,
    @IdFactura    INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT N.id_nota, N.FACTURA_id_factura AS id_factura, F.numero_factura, N.numero_nota, N.cuf, N.fecha_emision,
           F.nombre_razon_social, F.numero_documento, N.monto_total_original, N.monto_total_devuelto,
           N.monto_efectivo_credito_debito, N.motivo, N.reingreso_stock, N.estado, N.codigo_estado, N.mensajes,
           N.fecha_anulacion, MA.descripcion AS motivo_anulacion
    FROM FACTURACION.NOTA_CREDITO_DEBITO N
    INNER JOIN FACTURACION.FACTURA F ON F.id_factura = N.FACTURA_id_factura
    LEFT JOIN FACTURACION.CATALOGO MA ON MA.tipo = 'MOTIVO_ANULACION' AND MA.codigo = CAST(N.codigo_motivo_anulacion AS VARCHAR(20))
    WHERE (@FechaInicio IS NULL OR CAST(N.fecha_emision AS DATE) >= @FechaInicio)
      AND (@FechaFin IS NULL OR CAST(N.fecha_emision AS DATE) <= @FechaFin)
      AND (@IdFactura IS NULL OR N.FACTURA_id_factura = @IdFactura)
    ORDER BY N.id_nota DESC;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.ObtenerNota
    @IdNota  INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT N.*, MA.descripcion AS motivo_anulacion_descripcion
    FROM FACTURACION.NOTA_CREDITO_DEBITO N
    LEFT JOIN FACTURACION.CATALOGO MA ON MA.tipo = 'MOTIVO_ANULACION' AND MA.codigo = CAST(N.codigo_motivo_anulacion AS VARCHAR(20))
    WHERE N.id_nota = @IdNota;

    SELECT D.*, U.descripcion AS descripcion_unidad_medida
    FROM FACTURACION.NOTA_DETALLE D
    LEFT JOIN FACTURACION.CATALOGO U ON U.tipo = 'UNIDAD_MEDIDA' AND U.codigo = CAST(D.unidad_medida AS VARCHAR(20))
    WHERE D.NOTA_id_nota = @IdNota
    ORDER BY D.codigo_detalle_transaccion, D.id_nota_detalle;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.RegistrarAnulacionNota
    @IdNota        INT,
    @CodigoMotivo  INT,
    @CodigoEstado  INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
        UPDATE FACTURACION.NOTA_CREDITO_DEBITO
        SET estado = 'ANULADA', codigo_estado = @CodigoEstado, codigo_motivo_anulacion = @CodigoMotivo, fecha_anulacion = GETDATE()
        WHERE id_nota = @IdNota AND estado = 'VALIDADA';

        DECLARE @Filas INT = @@ROWCOUNT;

        -- Si la devolución había reingresado stock, se revierte.
        IF @Filas = 1
            UPDATE S
            SET S.cantidad = S.cantidad - CAST(D.cantidad AS INT), S.fecha_actualizacion = GETDATE()
            FROM INVENTARIO.STOCK S
            INNER JOIN FACTURACION.NOTA_CREDITO_DEBITO N ON N.id_nota = @IdNota AND N.reingreso_stock = 1
            INNER JOIN FACTURACION.NOTA_DETALLE D ON D.NOTA_id_nota = N.id_nota AND D.codigo_detalle_transaccion = 2
            WHERE S.TIENDA_id_tienda = N.TIENDA_id_tienda
              AND D.codigo_producto LIKE 'P-%'
              AND S.PRODUCTO_id_producto = TRY_CAST(SUBSTRING(D.codigo_producto, 3, 20) AS INT);
    COMMIT TRANSACTION;

    SELECT @Filas AS filas_afectadas;
END
GO

CREATE OR ALTER PROCEDURE FACTURACION.ContarNotasVigentes
    @IdFactura  INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) FROM FACTURACION.NOTA_CREDITO_DEBITO WHERE FACTURA_id_factura = @IdFactura AND estado = 'VALIDADA';
END
GO

PRINT 'Esquema FACTURACION instalado correctamente.';
GO
