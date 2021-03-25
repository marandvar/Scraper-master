using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Scraper.Web.Models
{
    public class WebViewModel
    {
        public string Comuna { get; set; }
        public string Concatenar { get; set; }
        public string Url { get; set; }
        public string UrlBusqueda { get; set; }
        public bool UrlBusquedaColumnasSeparadas { get; set; }
        public string UrlBusquedaPaginada { get; set; }
        public string UrlBusquedaPaginada2 { get; set; }
        public bool Ajax { get; set; }
        public Dictionary<string, string> Params { get; set; }
        public string Selector { get; set; }
        public string Selector2 { get; set; }
        public string SelectorNonce { get; set; }
        public string SelectorEventValidation { get; set; }
        public string SelectorState { get; set; }
        public bool Json { get; set; }
        public string PaginaAjax { get; set; }
        public string AnioAjax { get; set; }
        public bool AjaxTablaDinamica { get; set; }
        public int? InicioPagina { get; set; }
        public string AtributoJson { get; set; }
        public bool Token { get; set; }
        public bool Token2 { get; set; }
    }
}
