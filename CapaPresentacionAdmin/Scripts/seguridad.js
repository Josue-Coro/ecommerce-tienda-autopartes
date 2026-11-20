/* Protección XSS en el cliente.
 * - window.esc(texto): escapa texto para insertarlo como HTML.
 * - Por defecto, toda columna de DataTables sin "render" propio muestra el texto escapado.
 *   Las columnas con "render" propio deben usar esc() para los datos que vienen de la BD. */
(function (w, $) {
    var mapa = { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;", "`": "&#96;" };
    w.esc = function (t) { return t == null ? "" : String(t).replace(/[&<>"'`]/g, function (c) { return mapa[c]; }); };
    if ($ && $.fn && $.fn.dataTable) {
        $.extend(true, $.fn.dataTable.defaults, {
            columnDefs: [{
                targets: "_all",
                render: function (d, type) {
                    if (type !== "display") return d;
                    if (typeof d === "string") return w.esc(d);
                    return d;
                }
            }]
        });
    }
})(window, window.jQuery);
