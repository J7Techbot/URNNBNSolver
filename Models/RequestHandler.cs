using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace URNNBNSolver.Models
{
    public class RequestHandler
    {
        private static HttpClient _client = new HttpClient();
        public ObservableCollection<string> ProtocolCollection { get; set; }

        public string ServerURL = "https://resolver-test.nkp.cz/api/v4";
        public string Sigla = "ex001";
        private string _login = "exon";
        private string _psw = "tpL9zsk9";

        public async Task<XDocument> CreateRequest(Dictionary<string, string> metaDict,bool withPredecessor)
        {
            XNamespace mNS = XNamespace.Get("http://resolver.nkp.cz/v4/");
            XDocument request = new XDocument();

            string mainEle = "";
            string title = "";
            string subtitle = "";

            if (metaDict["projectType"].ToLower() == "periodical")
            {
                mainEle = "periodicalIssue";
                title = "periodicalTitle";
                subtitle = "issueTitle";
            }
            else
            {
                mainEle = "monographVolume";
                title = "monographTitle";
                subtitle = "volumeTitle";
            }

            XElement root =
                new XElement(mNS + "import",
                    new XElement(mNS + mainEle,
                        new XElement(mNS + "titleInfo",
                            new XElement(mNS + title, metaDict["title"]))),
                    new XElement(mNS + "digitalDocument"));

            XElement main = root.Element(mNS + mainEle);

            if (metaDict["subTitle"] != null)
                root.Descendants(mNS + title).First().AddAfterSelf(new XElement(mNS + subtitle, metaDict["subTitle"]));
            else
                root.Descendants(mNS + title).First().AddAfterSelf(new XElement(mNS + subtitle, "default"));

            if (metaDict["ccnb"] != null)
                main.Add(new XElement(mNS + "ccnb", metaDict["ccnb"]));
            if (metaDict["isnb"] != null)
                main.Add(new XElement(mNS + "isnb", metaDict["isnb"]));
            if (metaDict["documentType"] != null)
                main.Add(new XElement(mNS + "documentType", metaDict["documentType"]));
            if (metaDict["digitalBorn"] != null)
                main.Add(new XElement(mNS + "digitalBorn", metaDict["digitalBorn"]));
            if (metaDict["primaryOriginatorName"] != null)
                main.Add(new XElement(mNS + "primaryOriginator", metaDict["primaryOriginatorName"], new XAttribute("type", "AUTHOR")));

            main.Add(new XElement(mNS + "publication"));
            var publication = main.Element(mNS + "publication");

            if (metaDict["publisher"] != null)
                publication.Add(new XElement(mNS + "publisher", metaDict["publisher"]));
            if (metaDict["place"] != null)
                publication.Add(new XElement(mNS + "place", metaDict["place"]));
            if (metaDict["year"] != null)
                publication.Add(new XElement(mNS + "year", metaDict["year"]));

            XElement digitalDocument = root.Element(mNS + "digitalDocument");

            digitalDocument.Add(new XElement(mNS + "archiverId", "1"));

            if (withPredecessor)
            {
                await Send4DeleteIdentifiers(metaDict["urnnbn"]);

                AddToProtocol("Registrování s následnickou vazbou.");

                digitalDocument.Add(new XElement(mNS + "urnNbn"));
                digitalDocument.Element(mNS + "urnNbn").Add(new XElement(mNS + "predecessor", new XAttribute("value", metaDict["urnnbn"]), new XAttribute("note", "test note")));
            }
            else
            {
                AddToProtocol("Registrování bez vazby.");

                if (await Send4DeleteIdentifiers(metaDict["urnnbn"]))
                    await Send4DeleteURNNBN(metaDict["urnnbn"]);
            }

            digitalDocument.Add(new XElement(mNS + "registrarScopeIdentifiers"));

            digitalDocument.Element(mNS + "registrarScopeIdentifiers").Add(new XElement(mNS + "id", metaDict["uuid"], new XAttribute("type", "K4_pid")));

            if (metaDict["financed"] != null)
                digitalDocument.Add(new XElement(mNS + "financed", metaDict["financed"]));
            if (metaDict["contractNumber"] != null)
                digitalDocument.Add(new XElement(mNS + "contractNumber", metaDict["contractNumber"]));

            request.Add(root);
            return request;
        }
        public async Task<string> Send4RegisterURNNBN(XDocument request)
        {
            try
            {
                //https://resolver-test.nkp.cz/api/v4/registrars/ex001/digitalDocuments
                string url = ServerURL + "/registrars/" + Sigla + "/digitalDocuments";

                Authorize();

                var stringContent = new StringContent(request.ToString(), Encoding.UTF8, "application/xml");
                var response = await _client.PostAsync(url, stringContent);

                string responseBody = await response.Content.ReadAsStringAsync();

                XNamespace mNS = XNamespace.Get("http://resolver.nkp.cz/v4/");
                responseBody = XDocument.Parse(responseBody).Descendants(mNS + "value").FirstOrDefault()?.Value.ToString();

                if (responseBody == null)
                {
                    AddToProtocol("UUID již bylo registrováno u jiného dokumentu.");
                }
                else
                    AddToProtocol("Nové URNNBN zaregistrováno.", responseBody, "This", "Webová služba nedokázala odpovědět, zkuste to prosím znovu.");

                return responseBody;
            }
            catch
            {
                MessageBox.Show("Vyskytl se neočekávaný problém při registraci nového urnnbn!");
                return null;
            }
        }
        public async Task<bool> Send4DeleteURNNBN(string urnnbn)
        {
            try
            {
                //https://resolver-test.nkp.cz/api/v4/urnnbn/urn:nbn:cz:ex001-0000xy

                Authorize();

                string _urnnbn = urnnbn;

                string addr = ServerURL + "/urnnbn/" + _urnnbn;

                HttpResponseMessage response = await _client.DeleteAsync(addr);

                string responseBody = await response.Content.ReadAsStringAsync();

                AddToProtocol("Původní URNNBN bylo deaktivováno.", responseBody, "This", "Webová služba nedokázala odpovědět, zkuste to prosím znovu.");

                return true;
            }
            catch
            {
                MessageBox.Show("Vyskytl se neočekávaný problém při deaktivaci starého urnnbn!");
                return false;
            }

        }
        public async Task<bool> Send4DeleteIdentifiers(string urnnbn)
        {
            try
            {
                //https://resolver-test.nkp.cz/api/v4/resolver/urn:nbn:cz:ex001-0000y3/registrarScopeIdentifiers

                Authorize();

                string _urnnbn = urnnbn;

                string addr = ServerURL + "/resolver/" + _urnnbn + "/registrarScopeIdentifiers";

                HttpResponseMessage response = await _client.DeleteAsync(addr);

                string responseBody = await response.Content.ReadAsStringAsync();

                AddToProtocol("Identifikátory na předchůdci byly deaktivovány.", responseBody, "This", "Webová služba nedokázala odpovědět, zkuste to prosím znovu.");

                return true;
            }
            catch
            {
                MessageBox.Show("Vyskytl se neočekávaný problém při odtraňování identifikátorů!");
                return false;
            }            
        }
        private void Authorize()
        {
            var byteArray = Encoding.ASCII.GetBytes(_login + ":" + _psw);
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        }
        private void AddToProtocol(string protocolText, string responseText = null, string ifStartWith = null, string elseText = null)
        {
            if (ifStartWith == null)
                ProtocolCollection.Add("->" + protocolText);
            else
            {
                if (responseText.StartsWith(ifStartWith))
                    ProtocolCollection.Add("->" + elseText);
                else
                    ProtocolCollection.Add("->" + protocolText);
            }
        }
    }
}
