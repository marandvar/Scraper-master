
using AngleSharp;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using Scraper.Web.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Scraper.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            HttpContext.Session.SetString("ListaSession", System.Text.Json.JsonSerializer.Serialize(new List<PersonaViewModel>()));
            return View();
        }

        public async Task<IActionResult> Log()
        {
            DirectoryInfo di = new DirectoryInfo(@".\logs\");
            var lista = new List<LogViewModel>();
            if (di.Exists) 
            {
                foreach (var file in di.GetFiles()) 
                {
                    var vm = new LogViewModel() {
                        Nombre = file.Name,
                        Url = file.FullName,
                        Fecha = file.CreationTime
                    };
                    lista.Add(vm);
                }
            }
            //var logs =  
            return View(lista);
        }

        public async Task<IActionResult> LimpiarVariable() 
        {
            HttpContext.Session.Clear();
            return Ok();
        }

        public async Task<IActionResult> Resumen()
        {
            var listar = new List<PersonaViewModelAgrupado>();
            var sessionString = HttpContext.Session.GetString("ListaSession");
            if (sessionString != null)
            {
                var encontrados = System.Text.Json.JsonSerializer.Deserialize<List<PersonaViewModel>>(sessionString);
                if (encontrados.Count > 0) 
                {
                    listar = encontrados.GroupBy(n => n.Conservador).Select(n => new PersonaViewModelAgrupado {  Nombre = n.Key, Cantidad = n.Count() }).ToList();          
                }
            }
            return View(listar);
        }
        public async Task<IActionResult> InicializarSession() 
        {
            HttpContext.Session.SetString("ListaSession", System.Text.Json.JsonSerializer.Serialize(new List<PersonaViewModel>()));
            return Ok();
        }
        public async Task<int> ListasParciales() 
        {
            var listaSession = 0;
            var sessionString = HttpContext.Session.GetString("ListaSession");
            if (sessionString != null)
            {
                listaSession = System.Text.Json.JsonSerializer.Deserialize<List<PersonaViewModel>>(sessionString).Count;
            }
            return listaSession;
        }
        public async Task<IActionResult> Busqueda(string busquedaNombres, string busquedaApellidos, string archivo)
        {
            //HttpContext.Session.Clear();
            var lista = new List<PersonaViewModel>();

            var result = LeerArchivo(archivo);

            Stopwatch timeTotal = new Stopwatch(); // Creación del Stopwatch.
            timeTotal.Start();

            long maxTime = 0;

            foreach (var pagina in result)
            {
                var busqueda = busquedaNombres + " " + busquedaApellidos;
                if (pagina.Concatenar == "nombres_apellidos")
                    busqueda = busquedaNombres + " " + busquedaApellidos;
                if (pagina.Concatenar == "apellidos_nombres")
                    busqueda = busquedaApellidos + " " + busquedaNombres;
                if (pagina.Concatenar == "separados")
                    busqueda = busquedaApellidos + "/" + busquedaNombres;
                busqueda = busqueda.Trim();
       

                lista.AddRange(await BusquedaPagina(pagina, busqueda));
            }

            timeTotal.Stop();
            _logger.LogInformation("Tiempo total transcurrido: {0}", timeTotal.Elapsed.ToString("hh\\:mm\\:ss\\.fff"));
            _logger.LogInformation("Tiempo maximo en una consulta: {0}", maxTime);

            if (lista.Count != 0)
            {
                AddListaSession(lista);
            }
            return View(lista);
        }

        public void AddListaSession(List<PersonaViewModel> lista)
        {
            var sessionString = HttpContext.Session.GetString("ListaSession");
            if (sessionString != null)
            {
                var listaSession = System.Text.Json.JsonSerializer.Deserialize<List<PersonaViewModel>>(sessionString);
                listaSession.AddRange(lista);
                HttpContext.Session.SetString("ListaSession", System.Text.Json.JsonSerializer.Serialize(listaSession));
            }
            else
            {
                HttpContext.Session.SetString("ListaSession", System.Text.Json.JsonSerializer.Serialize(lista));
            }
        }

        public async Task<List<PersonaViewModel>> BusquedaPagina(WebViewModel pagina, string busqueda) 
        {
            var lista = new List<PersonaViewModel>();

            Stopwatch timeTotal = new Stopwatch(); // Creación del Stopwatch.
            timeTotal.Start();

            long maxTime = 0;

            _logger.LogInformation($"Inicio Busqueda Fecha[" + DateTime.Now + "] -  Conservador[" + pagina.Comuna + "] - Nombre[" + busqueda + "]");
            Stopwatch sw = new Stopwatch(); // Creación del Stopwatch.
            if (!string.IsNullOrEmpty(pagina.UrlBusqueda) && pagina.Ajax == false && pagina.UrlBusquedaColumnasSeparadas == false)
            {
                try
                {
                    sw.Start();
                    lista.AddRange(await BusquedaUrl(pagina, busqueda));
                    sw.Stop();
                    _logger.LogInformation("Tiempo transcurrido: {0}", sw.Elapsed.ToString("hh\\:mm\\:ss\\.fff"));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error Busqueda Fecha[" + DateTime.Now + "] -  Conservador[" + pagina.Comuna + "] - Nombre[" + busqueda + "] - Error[" + ex.Message + "]");

                }
            }
            if (pagina.UrlBusquedaColumnasSeparadas == true && pagina.Ajax == false)
            {
                try
                {
                    sw.Start();
                    lista.AddRange(await BusquedaUrlColumnasSeparadas(pagina, busqueda));
                    sw.Stop();
                    _logger.LogInformation("Tiempo transcurrido: {0}", sw.Elapsed.ToString("hh\\:mm\\:ss\\.fff"));

                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error Busqueda Fecha[" + DateTime.Now + "] -  Conservador[" + pagina.Comuna + "] - Nombre[" + busqueda + "] - Error[" + ex.Message + "]");

                }

            }
            if (!string.IsNullOrEmpty(pagina.UrlBusquedaPaginada))
            {
                try
                {
                    sw.Start();
                    lista.AddRange(await BusquedaPaginada(pagina, busqueda));
                    sw.Stop();
                    _logger.LogInformation("Tiempo transcurrido: {0}", sw.Elapsed.ToString("hh\\:mm\\:ss\\.fff"));

                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error Busqueda Fecha[" + DateTime.Now + "] -  Conservador[" + pagina.Comuna + "] - Nombre[" + busqueda + "] - Error[" + ex.Message + "]");

                }
            }
            if (!string.IsNullOrEmpty(pagina.UrlBusquedaPaginada2))
            {
                try
                {
                    sw.Start();
                    lista.AddRange(await BusquedaPaginada2(pagina, busqueda));
                    sw.Stop();
                    _logger.LogInformation("Tiempo transcurrido: {0}", sw.Elapsed.ToString("hh\\:mm\\:ss\\.fff"));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error Busqueda Fecha[" + DateTime.Now + "] -  Conservador[" + pagina.Comuna + "] - Nombre[" + busqueda + "] - Error[" + ex.Message + "]");

                }
            }
            if (string.IsNullOrEmpty(pagina.PaginaAjax) && string.IsNullOrEmpty(pagina.AnioAjax) && pagina.Ajax == true && pagina.AjaxTablaDinamica == false && pagina.Token == false && pagina.Token2 == false)
            {
                try
                {
                    sw.Start();
                    lista.AddRange(await BusquedaAjax(pagina, busqueda));
                    sw.Stop();
                    _logger.LogInformation("Tiempo transcurrido: {0}", sw.Elapsed.ToString("hh\\:mm\\:ss\\.fff"));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error Busqueda Fecha[" + DateTime.Now + "] -  Conservador[" + pagina.Comuna + "] - Nombre[" + busqueda + "] - Error[" + ex.Message + "]");

                }
            }
            if (!string.IsNullOrEmpty(pagina.PaginaAjax))
            {
                try
                {
                    sw.Start();
                    lista.AddRange(await BusquedaAjaxPaginada(pagina, busqueda));
                    sw.Stop();
                    _logger.LogInformation("Tiempo transcurrido: {0}", sw.Elapsed.ToString("hh\\:mm\\:ss\\.fff"));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error Busqueda Fecha[" + DateTime.Now + "] -  Conservador[" + pagina.Comuna + "] - Nombre[" + busqueda + "] - Error[" + ex.Message + "]");

                }
            }
            if (!string.IsNullOrEmpty(pagina.AnioAjax))
            {
                try
                {
                    sw.Start();
                    lista.AddRange(await BusquedaAjaxPorAnio(pagina, busqueda));
                    sw.Stop();
                    _logger.LogInformation("Tiempo transcurrido: {0}", sw.Elapsed.ToString("hh\\:mm\\:ss\\.fff"));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error Busqueda Fecha[" + DateTime.Now + "] -  Conservador[" + pagina.Comuna + "] - Nombre[" + busqueda + "] - Error[" + ex.Message + "]");

                }
            }
            if (pagina.AjaxTablaDinamica)
            {
                try
                {
                    sw.Start();
                    lista.AddRange(await BusquedaAjaxTablaDinamica(pagina, busqueda));
                    sw.Stop();
                    _logger.LogInformation("Time elapsed: {0}", sw.Elapsed.ToString("hh\\:mm\\:ss\\.fff"));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error Busqueda Fecha[" + DateTime.Now + "] -  Conservador[" + pagina.Comuna + "] - Nombre[" + busqueda + "] - Error[" + ex.Message + "]");

                }
            }
            if (pagina.Token == true)
            {
                try
                {
                    sw.Start();
                    lista.AddRange(await BusquedaAjaxToken(pagina, busqueda));
                    sw.Stop();
                    _logger.LogInformation("Time elapsed: {0}", sw.Elapsed.ToString("hh\\:mm\\:ss\\.fff"));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error Busqueda Fecha[" + DateTime.Now + "] -  Conservador[" + pagina.Comuna + "] - Nombre[" + busqueda + "] - Error[" + ex.Message + "]");

                }
            }
            if (pagina.Token2 == true)
            {
                try
                {
                    sw.Start();
                    lista.AddRange(await BusquedaAjaxToken2(pagina, busqueda));
                    sw.Stop();
                    _logger.LogInformation("Time elapsed: {0}", sw.Elapsed.ToString("hh\\:mm\\:ss\\.fff"));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error Busqueda Fecha[" + DateTime.Now + "] -  Conservador[" + pagina.Comuna + "] - Nombre[" + busqueda + "] - Error[" + ex.Message + "]");

                }
            }
            maxTime = sw.ElapsedMilliseconds > maxTime ? sw.ElapsedMilliseconds : maxTime;
            _logger.LogInformation($"Fin Busqueda Fecha[" + DateTime.Now + "] -  Conservador[" + pagina.Comuna + "] - Nombre[" + busqueda + "] - Resultados Encontrados[" + lista.Count() + "]");

            return lista;
        }


        public async  Task<PartialViewResult> IndexResultado(string busquedaNombres, string busquedaApellidos, string conservador)
        {
            HttpContext.Session.Clear();
            var lista = new List<PersonaViewModel>();
            var conservadorActivo = "";
            var busquedaActiva = "";
            try
            {
                var result = LeerArchivo("paginasTodas.json");

                if (!string.IsNullOrEmpty(conservador)) 
                {
                    result = result.Where(a => a.Comuna == conservador).ToList();
                }
                Stopwatch timeTotal = new Stopwatch(); // Creación del Stopwatch.
                timeTotal.Start();

                long maxTime = 0;

                foreach (var pagina in result)
                {
                    conservadorActivo = pagina.Comuna;

                    var busqueda = busquedaNombres + " " + busquedaApellidos;
                    if (pagina.Concatenar == "nombres_apellidos")
                        busqueda = busquedaNombres + " " + busquedaApellidos;
                    if (pagina.Concatenar == "apellidos_nombres")
                        busqueda = busquedaApellidos + " " + busquedaNombres;
                    if (pagina.Concatenar == "separados")
                        busqueda = busquedaApellidos + "/" + busquedaNombres;
                    busqueda = busqueda.Trim();
                    busquedaActiva = busqueda;

                    lista.AddRange(await BusquedaPagina(pagina, busqueda));

                   
                }
                timeTotal.Stop();
                _logger.LogInformation("Tiempo total transcurrido: {0}", timeTotal.Elapsed.ToString("hh\\:mm\\:ss\\.fff"));
                _logger.LogInformation("Tiempo maximo en una consulta: {0}", maxTime);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error Busqueda Fecha[" + DateTime.Now + "] -  Conservador[" + conservadorActivo + "] - Nombre[" + busquedaActiva + "] - Error[" + ex.Message + "]");
                throw ex;
            }     
    
            HttpContext.Session.SetString("ListaSession", System.Text.Json.JsonSerializer.Serialize(lista));

            _logger.LogInformation("Registro total: {0}", lista.Count());
            return PartialView("_IndexResultado", lista);
        }

        public IActionResult Exportar() 
        {
            string fileName = "Lista_Conservadores.xlsx";
            string fileType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            var stream = new MemoryStream();
            try
            {
                var sessionString = HttpContext.Session.GetString("ListaSession");
                var lista = System.Text.Json.JsonSerializer.Deserialize<List<PersonaViewModel>>(sessionString);

                using (var excelPackage = new ExcelPackage(stream)) 
                {
                    var workSheet = excelPackage.Workbook.Worksheets.Add("Listado de Busqueda");
                    workSheet.Cells.LoadFromCollection(lista, true);
                    excelPackage.Save();
                }
                stream.Position = 0;
            }
            catch (Exception ex)
            {

                throw ex;
            }

            return File(stream, fileType, fileName);
        }


        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public List<WebViewModel> LeerArchivo(string archivo)
        {
            if (archivo == null)
                return null;
            var jsonString =  System.IO.File.ReadAllText(archivo);
           // var jsonString = System.IO.File.ReadAllText("pruebas.json");
            var result = JsonSerializer.Deserialize<List<WebViewModel>>(jsonString);
            return result;

        }
        public async Task<List<PersonaViewModel>> BusquedaAjax(WebViewModel pagina, string busqueda)
        {
            var lista = new List<PersonaViewModel>();
            var apellido = "";
            var nombre = "";
            string[] nonceSplit;
            var nonce = "";
            var separados = false;
            if (busqueda.Contains("/"))
            {
                apellido = busqueda.Split('/')[0];
                nombre = busqueda.Split('/')[1];
                separados = true;
            }
            using (var client = new HttpClient())
            {
                var parametros = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(pagina.SelectorNonce))
                {
                    var llamadoGet = await client.GetAsync(pagina.Url);
                    var llamadoGetContent = await llamadoGet.Content.ReadAsStringAsync();
                    var htmlDocGet = new HtmlDocument();
                    htmlDocGet.LoadHtml(llamadoGetContent);

                    var nonce2 = htmlDocGet.DocumentNode.SelectNodes(pagina.SelectorNonce);
                    nonceSplit = nonce2[0].OuterHtml.Split("value=");
                    nonce = nonceSplit[1].Substring(1, 10);
                }
                foreach (var item in pagina.Params.ToList())
                {
                    if (separados)
                    {
                        parametros.Add(item.Key, item.Value.Replace("{0}", apellido).Replace("{1}", nombre).Replace("{nonce}", nonce).Replace("{aniofin}", DateTime.Now.Year.ToString()));
                    }
                    else
                    {
                        parametros.Add(item.Key, item.Value.Replace("{0}", busqueda).Replace("{5}", "Ñ").Replace("{nonce}", nonce).Replace("{aniofin}", DateTime.Now.Year.ToString()));
                    }
                }

                var content = new FormUrlEncodedContent(parametros);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                var response = await client.PostAsync(pagina.UrlBusqueda, content);
                //var response = client.GetAsync(pagina.UrlBusqueda).Result;
                if (response.IsSuccessStatusCode)
                {
                    var htmlBody = await response.Content.ReadAsStringAsync();
                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(htmlBody);

                    if (pagina.Json)
                    {
                        var jsonn = JObject.Parse(htmlBody);
                        if (jsonn[pagina.AtributoJson].ToString() != "No se han encontrado coincidencias" && jsonn[pagina.AtributoJson].ToString() != "0")
                        {
                            if (pagina.Url == "https://cbrlosandes.cl/consulta-propiedad/")
                            {
                                for (int i = 0; i < int.Parse(jsonn[pagina.AtributoJson].ToString()); i++)
                                {
                                    var nuevo = new PersonaViewModel();
                                    //System.Diagnostics.Debug.WriteLine(i);
                                    nuevo.Nombre = jsonn["data"][i][1].ToString();
                                    nuevo.Conservador = pagina.Comuna;
                                    nuevo.Url = pagina.Url;
                                    lista.Add(nuevo);
                                }
                            }
                            else
                            {
                                foreach (var item in jsonn["content"]["list"])
                                {
                                    var nuevo = new PersonaViewModel();
                                    nuevo.Nombre = item["nombre"].ToString().Replace("{", "").Replace("}", "");
                                    nuevo.Conservador = pagina.Comuna;
                                    nuevo.Url = pagina.Url;
                                    lista.Add(nuevo);
                                }
                            }
                        }
                    }
                    else
                    {
                        var nodos = htmlDoc.DocumentNode.SelectNodes(pagina.Selector);

                        if (nodos != null)
                        {
                            foreach (var nodo in nodos)
                            {

                                if (nodo.InnerText.ToUpper().Contains("COMPRADOR") || string.IsNullOrEmpty(nodo.InnerHtml))
                                    continue;
                                var nuevo = new PersonaViewModel();
                                nuevo.Nombre = nodo.InnerText.Replace("&nbsp;", "");
                                nuevo.Conservador = pagina.Comuna;
                                nuevo.Url = pagina.Url;
                                lista.Add(nuevo);
                            }
                        }
                    }
                }
            }
            return lista;
        }
        public async Task<List<PersonaViewModel>> BusquedaAjaxPaginada(WebViewModel pagina, string busqueda)
        {
            var lista = new List<PersonaViewModel>();
            var apellido = "";
            var nombre = "";
            var separados = false;
            if (busqueda.Contains("/"))
            {
                apellido = busqueda.Split('/')[0];
                nombre = busqueda.Split('/')[1];
                separados = true;
            }
            using (var client = new HttpClient())
            {

                for (int i = pagina.InicioPagina != null ? pagina.InicioPagina.Value : 0; i < 1000; i = i + int.Parse(pagina.PaginaAjax))
                {
                    var parametros = new Dictionary<string, string>();
                    foreach (var item in pagina.Params.ToList())
                    {
                        if (separados)
                        {
                            parametros.Add(item.Key, item.Value.Replace("{0}", apellido).Replace("{1}", nombre).Replace("{pagina}", i.ToString()).Replace("{aniofin}", DateTime.Now.Year.ToString()));
                        }
                        else
                        {
                            parametros.Add(item.Key, item.Value.Replace("{0}", busqueda).Replace("{pagina}", i.ToString()).Replace("{aniofin}", DateTime.Now.Year.ToString()));
                        }
                    }
                    var content = new FormUrlEncodedContent(parametros);
                    var response = await client.PostAsync(pagina.UrlBusqueda.Replace("{pagina}", i.ToString()), content);
                    //var response = client.GetAsync(pagina.UrlBusqueda).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        var htmlBody = await response.Content.ReadAsStringAsync();
                        var htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(htmlBody);

                        var nodos = htmlDoc.DocumentNode.SelectNodes(pagina.Selector);
                        if (pagina.UrlBusquedaColumnasSeparadas)
                        {
                            var nodos2 = htmlDoc.DocumentNode.SelectNodes(pagina.Selector2);
                            if (nodos2 != null)
                            {

                                for (int j = 0; j < nodos2.Count; j++)
                                {
                                    var nuevo = new PersonaViewModel();
                                    nuevo.Nombre = nodos[j].InnerText + " " + nodos2[j].InnerHtml;
                                    nuevo.Conservador = pagina.Comuna;
                                    nuevo.Url = pagina.Url;
                                    lista.Add(nuevo);
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                        else
                        {
                            if (nodos != null)
                            {
                                foreach (var nodo in nodos)
                                {
                                    if (nodo.InnerText.ToUpper().Contains("COMPRADOR") || nodo.InnerText.ToUpper().Contains("PERSONA") || nodo.InnerText.ToUpper().Contains("PROPIETARIO"))
                                    {
                                        if (nodos.Count() == 1)
                                        {
                                            i = 1000;
                                            break;
                                        }
                                        continue;
                                    }
                                    var nuevo = new PersonaViewModel();
                                    nuevo.Nombre = nodo.InnerText;
                                    nuevo.Conservador = pagina.Comuna;
                                    nuevo.Url = pagina.Url;
                                    lista.Add(nuevo);
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }
            return lista;
        }

        public async Task<List<PersonaViewModel>> BusquedaAjaxPorAnio(WebViewModel pagina, string busqueda)
        {
            var lista = new List<PersonaViewModel>();

            using (var client = new HttpClient())
            {
                for (int i = 1980; i <= DateTime.Now.Year; i++)
                {
                    var parametros = new Dictionary<string, string>();
                    foreach (var item in pagina.Params.ToList())
                    {
                        parametros.Add(item.Key, item.Value.Replace("{0}", busqueda).Replace("{anio}", i.ToString()));
                    }
                    var content = new FormUrlEncodedContent(parametros);
                    var response = await client.PostAsync(pagina.UrlBusqueda, content);
                    if (response.IsSuccessStatusCode)
                    {
                        var htmlBody = await response.Content.ReadAsStringAsync();
                        var jsonn = JObject.Parse(htmlBody);
                        if (jsonn["obj"]["recordsTotal"].ToString() != "0")
                        {
                            foreach (var item in jsonn["obj"]["data"])
                            {
                                //System.Diagnostics.Debug.WriteLine(i);
                                var nuevo = new PersonaViewModel();
                                nuevo.Nombre = item["titular"].ToString();
                                nuevo.Conservador = pagina.Comuna;
                                nuevo.Url = pagina.Url;
                                lista.Add(nuevo);
                            }
                        }
                    }
                }
            }
            return lista;
        }
        public async Task<List<PersonaViewModel>> BusquedaUrl(WebViewModel pagina, string busqueda)
        {
            var lista = new List<PersonaViewModel>();

            HtmlWeb web = new HtmlWeb();
            var htmlDoc = new HtmlDocument();
            var apellido = "";
            var nombre = "";
            if (busqueda.Contains("/"))
            {
                apellido = busqueda.Split('/')[0];
                nombre = busqueda.Split('/')[1];
                htmlDoc = await web.LoadFromWebAsync(pagina.UrlBusqueda.Replace("{0}", apellido).Replace("{1}", nombre).Replace("{aniofin}", DateTime.Now.Year.ToString()));
            }
            else
            {
                htmlDoc = await web.LoadFromWebAsync(pagina.UrlBusqueda.Replace("{0}", busqueda).Replace("{aniofin}", DateTime.Now.Year.ToString()));
            }
            var nodos = htmlDoc.DocumentNode.SelectNodes(pagina.Selector);

            if (nodos != null)
            {
                foreach (var nodo in nodos)
                {
                    if (nodo.InnerText.ToUpper().Contains("NOMBRE") || nodo.InnerText.ToUpper().Contains("PERSONA") || nodo.InnerText.ToUpper().Contains("PATERNO") || string.IsNullOrEmpty(nodo.InnerText))
                        continue;
                    var nuevo = new PersonaViewModel();
                    nuevo.Nombre = nodo.InnerText;
                    nuevo.Conservador = pagina.Comuna;
                    nuevo.Url = busqueda.Contains("/") ? pagina.UrlBusqueda.Replace("{0}", apellido).Replace("{1}", nombre).Replace("{aniofin}", DateTime.Now.Year.ToString()) : pagina.UrlBusqueda.Replace("{0}", busqueda).Replace("{aniofin}", DateTime.Now.Year.ToString());
                    lista.Add(nuevo);
                }
            }

            return lista;
        }
        public async Task<List<PersonaViewModel>> BusquedaUrlColumnasSeparadas(WebViewModel pagina, string busqueda)
        {
            var lista = new List<PersonaViewModel>();

            HtmlWeb web = new HtmlWeb();
            var htmlDoc = new HtmlDocument();
            var apellido = "";
            var nombre = "";
            if (busqueda.Contains("/"))
            {
                apellido = busqueda.Split('/')[0];
                nombre = busqueda.Split('/')[1];
                htmlDoc = web.Load(pagina.UrlBusqueda.Replace("{0}", apellido).Replace("{1}", nombre));
            }
            else
            {
                htmlDoc = await web.LoadFromWebAsync(pagina.UrlBusqueda.Replace("{0}", busqueda));
            }
            var nodos = htmlDoc.DocumentNode.SelectNodes(pagina.Selector);
            var nodos2 = htmlDoc.DocumentNode.SelectNodes(pagina.Selector2);

            if (nodos != null && nodos2 != null)
            {
                for (int i = 0; i < nodos.Count(); i++)
                {
                    if (nodos[i].InnerText.ToUpper().Contains("NOMBRE") || nodos[i].InnerText.ToUpper().Contains("PERSONA") || nodos[i].InnerText.ToUpper().Contains("PATERNO") || nodos[i].InnerText.ToUpper().Contains("APELLIDO"))
                        continue;
                    var nuevo = new PersonaViewModel();
                    nuevo.Nombre = nodos[i].InnerText + " " + nodos2[i].InnerText;
                    nuevo.Conservador = pagina.Comuna;
                    nuevo.Url = busqueda.Contains("/") ? pagina.UrlBusqueda.Replace("{0}", apellido).Replace("{1}", nombre) : pagina.UrlBusqueda.Replace("{0}", busqueda);
                    lista.Add(nuevo);
                }
            }

            return lista;
        }

        public async Task<List<PersonaViewModel>> BusquedaPaginada(WebViewModel pagina, string busqueda)
        {
            var lista = new List<PersonaViewModel>();
            for (int i = 0; i < 1000; i = i + 25)
            {
                HtmlWeb web = new HtmlWeb();
                var htmlDoc = await web.LoadFromWebAsync(pagina.UrlBusquedaPaginada.Replace("{0}", busqueda).Replace("lim=0", "lim=" + i.ToString()).Replace("{aniofin}", DateTime.Now.Year.ToString()));
                var nodos = htmlDoc.DocumentNode.SelectNodes(pagina.Selector);

                if (nodos != null)
                {
                    foreach (var nodo in nodos)
                    {
                        if (nodo.InnerText.ToUpper().Contains("NOMBRE") || nodo.InnerText.ToUpper().Contains("PERSONA"))
                            continue;

                        var nuevo = new PersonaViewModel();
                        nuevo.Nombre = nodo.InnerText;
                        nuevo.Conservador = pagina.Comuna;
                        nuevo.Url = pagina.UrlBusquedaPaginada.Replace("{0}", busqueda).Replace("lim=0", "lim=" + i.ToString()).Replace("{aniofin}", DateTime.Now.Year.ToString());
                        lista.Add(nuevo);
                    }
                }
                else
                {
                    break;
                }
            }

            return lista;
        }
        public async  Task<List<PersonaViewModel>> BusquedaPaginada2(WebViewModel pagina, string busqueda)
        {
            var lista = new List<PersonaViewModel>();
            for (int i = 1; i <= 40; i++)
            {
                HtmlWeb web = new HtmlWeb();
                var htmlDoc = new HtmlDocument();

                var apellido = "";
                var nombre = "";
                if (busqueda.Contains("/"))
                {
                    apellido = busqueda.Split('/')[0];
                    nombre = busqueda.Split('/')[1];
                    htmlDoc = await web.LoadFromWebAsync(pagina.UrlBusquedaPaginada2.Replace("{0}", apellido).Replace("{1}", nombre).Replace("pag=0", "pag=" + i.ToString()));
                }
                else
                {
                    htmlDoc = await web.LoadFromWebAsync(pagina.UrlBusquedaPaginada2.Replace("{0}", busqueda).Replace("pagina=0", "pagina=" + i.ToString()));

                }
                var nodos = htmlDoc.DocumentNode.SelectNodes(pagina.Selector);

                if (nodos != null)
                {
                    foreach (var nodo in nodos)
                    {
                        if (nodo.InnerText.ToUpper().Contains("NOMBRE") || nodo.InnerText.ToUpper().Contains("PERSONA") || string.IsNullOrEmpty(nodo.InnerText) || nodo.InnerText.Contains("\r\n"))
                        {
                            if (nodos.Count == 1)
                            {
                                i = 40;
                            }
                            continue;
                        }
                        var nuevo = new PersonaViewModel();
                        nuevo.Nombre = nodo.InnerText;
                        nuevo.Conservador = pagina.Comuna;
                        nuevo.Url = busqueda.Contains("/") ? (pagina.UrlBusquedaPaginada2.Replace("{0}", apellido).Replace("{1}", nombre).Replace("pag=0", "pag=" + i.ToString())) : (pagina.UrlBusquedaPaginada2.Replace("{0}", busqueda).Replace("pagina=0", "pagina=" + i.ToString()));
                        lista.Add(nuevo);
                    }
                }
                else
                {
                    break;
                }
            }

            return lista;
        }


        public async Task<List<PersonaViewModel>> BusquedaAjaxTablaDinamica(WebViewModel pagina, string busqueda)
        {
            var lista = new List<PersonaViewModel>();
            using (var client = new HttpClient())
            {
                var parametros = new Dictionary<string, string>();
                foreach (var item in pagina.Params.ToList())
                {
                    parametros.Add(item.Key, item.Value.Replace("{0}", busqueda));
                }

                var content = new FormUrlEncodedContent(parametros);

                var response = await client.PostAsync(pagina.UrlBusqueda, content);
                //var response = client.GetAsync(pagina.UrlBusqueda).Result;
                if (response.IsSuccessStatusCode)
                {
                    var htmlBody = await response.Content.ReadAsStringAsync();
                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(htmlBody);

                    var nodos = htmlDoc.DocumentNode.SelectNodes(pagina.Selector);

                    List<string> nodosFiltrados = new List<string>();

                    if (nodos != null)
                    {
                        foreach (var nodo in nodos)
                        {
                            if (nodo.InnerText.Contains("Nombre 1"))
                            {
                                nodosFiltrados.Add(nodo.InnerText.Replace("Nombre 1", "").Trim());
                            }
                        }
                    }
                    if (nodos != null)
                    {
                        foreach (var item in nodosFiltrados)
                        {

                            if (string.IsNullOrEmpty(item))
                                continue;
                            var nuevo = new PersonaViewModel();
                            nuevo.Nombre = item;
                            nuevo.Conservador = pagina.Comuna;
                            nuevo.Url = pagina.UrlBusqueda.Replace("{0}", busqueda);
                            lista.Add(nuevo);
                        }
                    }

                }
            }
            return lista;
        }

        public async Task<List<PersonaViewModel>> BusquedaAjaxToken(WebViewModel pagina, string busqueda)
        {
            var lista = new List<PersonaViewModel>();
            using (var client = new HttpClient())
            {
                var responseGet = await client.GetAsync(pagina.UrlBusqueda);
                var htmlBodyGet = await responseGet.Content.ReadAsStringAsync();

                var htmlDocGet = new HtmlDocument();
                htmlDocGet.LoadHtml(htmlBodyGet);

                var EVENTVALIDATIONSelector = htmlDocGet.DocumentNode.SelectNodes(pagina.SelectorEventValidation);
                var EVENTVALIDATIONSplit = EVENTVALIDATIONSelector[0].OuterHtml.Split("value=");
                var EVENTVALIDATION = EVENTVALIDATIONSplit[1].Substring(1, EVENTVALIDATIONSplit[1].Length - 3);

                var VIEWSTATESelector = htmlDocGet.DocumentNode.SelectNodes(pagina.SelectorState);
                var VIEWSTATESplit = VIEWSTATESelector[0].OuterHtml.Split("value=");
                var VIEWSTATE = VIEWSTATESplit[1].Substring(1, VIEWSTATESplit[1].Length - 3);

                var cookieContent = responseGet.Headers.FirstOrDefault(x => x.Key == "Set-Cookie").Value.First().Split(" ")[0];
                var tokenCookie = cookieContent.Split("=");
                var name = tokenCookie[0];
                var value = tokenCookie[1];
                for (int i = 1; i <= 100; i++)
                {
                    var parametros = new Dictionary<string, string>();

                    foreach (var item in pagina.Params.ToList())
                    {
                        if (i > 1)
                        {
                            if (item.Key == "ctl00$MainContent$bBuscar")
                                continue;
                        }
                        parametros.Add(item.Key.Replace("{4}", "ñ"), item.Value.Replace("{0}", busqueda).Replace("{pagina}", i.ToString()).Replace("{VIEWSTATE}", VIEWSTATE).Replace("{EVENTVALIDATION}", EVENTVALIDATION));
                    }

                    var content = new FormUrlEncodedContent(parametros);


                    content.Headers.Add("Cookie", $"{name}={value}");

                    var response = await client.PostAsync(pagina.UrlBusqueda, content);
                    if (response.IsSuccessStatusCode)
                    {
                        var htmlBody = await response.Content.ReadAsStringAsync();
                        var htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(htmlBody);
                        var nodos = htmlDoc.DocumentNode.SelectNodes(pagina.Selector);

                        if (nodos != null)
                        {

                            EVENTVALIDATIONSelector = htmlDoc.DocumentNode.SelectNodes(pagina.SelectorEventValidation);
                            EVENTVALIDATIONSplit = EVENTVALIDATIONSelector[0].OuterHtml.Split("value=");
                            VIEWSTATESelector = htmlDoc.DocumentNode.SelectNodes(pagina.SelectorState);
                            VIEWSTATESplit = VIEWSTATESelector[0].OuterHtml.Split("value=");
                            EVENTVALIDATION = EVENTVALIDATIONSplit[1].Substring(1, EVENTVALIDATIONSplit[1].Length - 3);
                            VIEWSTATE = VIEWSTATESplit[1].Substring(1, VIEWSTATESplit[1].Length - 3);

                            foreach (var nodo in nodos)
                            {
                                if (nodo.InnerText.ToUpper().Contains("COMPRADOR") || nodo.InnerText.ToUpper().Contains("&NBSP;"))
                                    continue;

                                var nuevo = new PersonaViewModel();
                                nuevo.Nombre = nodo.InnerText;
                                nuevo.Conservador = pagina.Comuna;
                                nuevo.Url = pagina.UrlBusqueda.Replace("{0}", busqueda);
                                lista.Add(nuevo);
                            }
                            if (nodos.Count < 11)
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return lista;
        }


        public async Task<List<PersonaViewModel>> BusquedaAjaxToken2(WebViewModel pagina, string busqueda)
        {
            var lista = new List<PersonaViewModel>();
            var apellido = busqueda.Split('/')[0];
            var nombre = busqueda.Split('/')[1];

            using (var client = new HttpClient())
            {
                var responseGet = await client.GetAsync(pagina.UrlBusqueda);
                var htmlBodyGet = await responseGet.Content.ReadAsStringAsync();

                var htmlDocGet = new HtmlDocument();
                htmlDocGet.LoadHtml(htmlBodyGet);

                var EVENTVALIDATIONSelector = htmlDocGet.DocumentNode.SelectNodes(pagina.SelectorEventValidation);
                var EVENTVALIDATIONSplit = EVENTVALIDATIONSelector[0].OuterHtml.Split("value=");
                var EVENTVALIDATION = EVENTVALIDATIONSplit[1].Substring(1, EVENTVALIDATIONSplit[1].Length - 3);

                var VIEWSTATESelector = htmlDocGet.DocumentNode.SelectNodes(pagina.SelectorState);
                var VIEWSTATESplit = VIEWSTATESelector[0].OuterHtml.Split("value=");
                var VIEWSTATE = VIEWSTATESplit[1].Substring(1, VIEWSTATESplit[1].Length - 3);

                var callBackSplit = htmlBodyGet.Split("callbackState':'");
                var callBackSplit2 = callBackSplit[1].Split("','");
                var callBackState = callBackSplit2[0];

                var cookieContent = responseGet.Headers.FirstOrDefault(x => x.Key == "Set-Cookie").Value.First().Split(" ")[0];
                var tokenCookie = cookieContent.Split("=");
                var name = tokenCookie[0];
                var value = tokenCookie[1];

                var parametros = new Dictionary<string, string>();

                foreach (var item in pagina.Params.ToList())
                {
                    parametros.Add(item.Key.Replace("{4}", "ñ"), item.Value.Replace("{0}", apellido).Replace("{1}", nombre).Replace("{VIEWSTATE}", VIEWSTATE).Replace("{EVENTVALIDATION}", EVENTVALIDATION).Replace("{callbackState}", callBackState));
                }

                var content = new FormUrlEncodedContent(parametros);


                content.Headers.Add("Cookie", $"{name}={value}");
                var response = await client.PostAsync(pagina.UrlBusqueda, content);
                if (response.IsSuccessStatusCode)
                {
                    var htmlBody = await response.Content.ReadAsStringAsync();
                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(htmlBody);
                    var nodos = htmlDoc.DocumentNode.SelectNodes(pagina.Selector);

                    if (nodos != null)
                    {
                        foreach (var nodo in nodos)
                        {
                            if (nodo.InnerText.ToUpper().Contains("COMPRADOR") || nodo.InnerText.ToUpper().Contains("&NBSP;") || nodo.InnerText.ToUpper().Contains("NOMBRE"))
                                continue;

                            var nuevo = new PersonaViewModel();
                            nuevo.Nombre = nodo.InnerText;
                            nuevo.Conservador = pagina.Comuna;
                            nuevo.Url = pagina.UrlBusqueda.Replace("{0}", busqueda);
                            lista.Add(nuevo);
                        }
                    }
                }
            }
            return lista;
        }
    }
}
