using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Scraper.Web.Models
{
    public class LogViewModel
    {
        public string Nombre { get; set; }
        public string Url { get; set; }
        public DateTime? Fecha { get; set; }
    }
}
