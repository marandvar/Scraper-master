using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Scraper.Web.Models
{
    public class PersonaViewModel
    {
        public string Nombre { get; set; }
        public string Conservador { get; set; }
        public string Url { get; set; }

        public int? Cantidad { get; set; }
    }

    public class PersonaViewModelAgrupado
    {
        public string Nombre { get; set; }
        public int? Cantidad { get; set; }
    }
}
