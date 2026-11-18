using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapaEntidad
{
    
    public class Usuario
    {
        public int id_usuario { get; set; }
        public string nombres { get; set; }
        public string apellidos { get; set; }
        public string correo { get; set; }
        public string contraseña { get; set; }
        public string telefono { get; set; }
        public bool estado { get; set; }
        public int id_rol { get; set; }
        public string rol { get; set; }
        public int id_tienda { get; set; }
        public string tienda { get; set; }

    }
    
}
