
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using URNNBNSolver.Shared;


namespace URNNBNSolver.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        #region Properties
        private static HttpClient _client = new HttpClient();
        private string[] _fileNames;

        private string confirmString;
        public string ConfirmString
        {
            get
            {
                return confirmString;
            }
            set
            {
                confirmString = value;
                OnPropertyChanged();
            }
        }

        private bool withPredecessor;
        public bool WithPredecessor
        {
            get
            {
                return withPredecessor;
            }
            set
            {
                withPredecessor = value;
                OnPropertyChanged();
            }
        }

        private bool keepOriginalMets;
        public bool KeepOriginalMets
        {
            get
            {
                return keepOriginalMets;
            }
            set
            {
                keepOriginalMets = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<string> protocolCollection;

        public ObservableCollection<string> ProtocolCollection
        {
            get
            {
                return protocolCollection;
            }
            set
            {
                protocolCollection = value;
                OnPropertyChanged();
            }
        }
        public List<string> Servs { get; set; }
        public int SelectedIndex { get; set; }

        string selectedServ = null;
        public string SelectedServ
        {
            get { return this.selectedServ; }
            set
            {
                if (value.StartsWith("Test"))
                {
                    _serverURL = "https://resolver-test.nkp.cz/api/v4";
                    _sigla = "ex001";
                }
                else
                {
                    _serverURL = "testChangeServer";
                    _sigla = "ex001";
                }


                this.selectedServ = value;
                
                OnPropertyChanged(); 
            }
        }


        private string _serverURL = "https://resolver-test.nkp.cz/api/v4";
        private string _sigla = "ex001";
        private string _login = "exon";
        private string _psw = "tpL9zsk9";
        #endregion

        public RelayCommand SelectMetsCommand { get; set; }
        public RelayCommand GenerateCommand { get; set; }

        public MainViewModel()
        {
            Servs = new List<string> { "Produkční server", "Test server"};
            SelectedIndex = 1;


            SelectMetsCommand = new RelayCommand(param => this.OnSelectMets(), param => true);
            GenerateCommand = new RelayCommand(param => this.OnGenerateURNNBN(), param => true);
        }
        private Dictionary<string, string> GetMeta(XDocument _xMets)
        {
            Dictionary<string, string> metas = new Dictionary<string, string>();

            XNamespace mNS = XNamespace.Get("http://www.loc.gov/mods/v3");
            XNamespace metsNS = XNamespace.Get("http://www.loc.gov/METS/");

            metas.Add("projectType", _xMets.Root.Attribute("TYPE").Value);
            metas.Add("title", _xMets.Descendants(mNS + "title").FirstOrDefault()?.Value);
            metas.Add("subTitle", _xMets.Descendants(mNS + "subTitle").FirstOrDefault()?.Value);
            metas.Add("ccnb", _xMets.Descendants(mNS + "ccnb").FirstOrDefault()?.Value); //ověřit
            metas.Add("isnb", _xMets.Descendants(mNS + "isnb").FirstOrDefault()?.Value); //ověřit
            metas.Add("documentType", _xMets.Descendants(mNS + "documentType").FirstOrDefault()?.Value); //ověřit
            metas.Add("digitalBorn", _xMets.Descendants(mNS + "digitalBorn").FirstOrDefault()?.Value); //ověřit            
            metas.Add("primaryOriginatorName", GetFullName(_xMets.Descendants(mNS + "name").Where(x => x.Attribute("type").Value == "personal" && x.Attribute("usage") != null && x.Attribute("usage").Value == "primary").FirstOrDefault())); //ověřit
            metas.Add("otherOriginatorName", GetFullName(_xMets.Descendants(mNS + "name").Where(x => x.Attribute("type").Value == "personal" && x.Attribute("usage") != null && x.Attribute("usage").Value == "primary").FirstOrDefault())); //ověřit
            metas.Add("publisher", _xMets.Descendants(mNS + "publisher").FirstOrDefault()?.Value);
            metas.Add("place", _xMets.Descendants(mNS + "placeTerm").Where(x => x.Attribute("type").Value == "text").FirstOrDefault()?.Value);
            metas.Add("year", _xMets.Descendants(mNS + "dateIssued").FirstOrDefault()?.Value);
            metas.Add("uuid", _xMets.Descendants(mNS + "identifier").Where(x => x.Attribute("type").Value == "uuid").FirstOrDefault()?.Value);
            metas.Add("urnnbn", _xMets.Descendants(mNS + "identifier").Where(x => x.Attribute("type").Value == "urnnbn").FirstOrDefault()?.Value);
            metas.Add("financed", _xMets.Descendants(mNS + "financed").FirstOrDefault()?.Value); //ověřit
            metas.Add("contractNumber", _xMets.Descendants(mNS + "contractNumber").FirstOrDefault()?.Value); //ověřit

            return metas;
        }
        private void OnSelectMets()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Multiselect = true;
            dlg.FileName = "Mets";
            dlg.DefaultExt = ".xml";
            dlg.Filter = "Mets documents (.xml)|*.xml";

            bool result = (bool)dlg.ShowDialog();

            if (result == true)
            {
                _fileNames = dlg.FileNames;
                ConfirmString = "Vybráno " + _fileNames.Length + " souborů.";

                
            }
        }
        private async void OnGenerateURNNBN()
        {
            if (_fileNames == null)
            {
                MessageBox.Show("Nejsou vybraná žádná mets!");
                return;
            }
            
            XNamespace mNS = XNamespace.Get("http://www.loc.gov/mods/v3");
            XNamespace dNS = XNamespace.Get("http://purl.org/dc/elements/1.1/");

            ProtocolCollection = new ObservableCollection<string>();

            AddToProtocol("Zpracováni probíha na " + _serverURL);

            for (int i = 0; i < _fileNames.Length; i++)
            {

                XDocument _xMets = XDocument.Load(_fileNames[i]);

                Dictionary<string,string> _metaDict = GetMeta(_xMets);

                XDocument _xReq = await CreateRequest(_metaDict);

                var urnnbn = await Send4RegisterURNNBN(_xReq);

                var urnNodesMods =_xMets.Descendants(mNS + "identifier").Where(x => x.Attribute("type").Value == "urnnbn");
                if (urnnbn != null)
                {
                    foreach (var node in urnNodesMods)
                    {
                        node.Value = urnnbn;
                    }

                    var urnNodesDC = _xMets.Descendants(dNS + "identifier").Where(x => x.Value.StartsWith("urn:nbn"));
                    foreach (var node in urnNodesDC)
                    {
                        node.Value = urnnbn;
                    }

                    if (KeepOriginalMets)
                    {                        
                        string dir = Path.GetDirectoryName(_fileNames[i]);
                        string original = dir + "/" + Path.GetFileNameWithoutExtension(_fileNames[i]) + "_original.xml";

                        if (File.Exists(original))
                        {
                            File.Delete(original);
                        }

                        File.Move(_fileNames[i], original);

                        _xMets.Save(_fileNames[i]);
                        AddToProtocol("Původní soubor byl zachován.");
                    }
                    else
                    {
                        _xMets.Save(_fileNames[i]);
                        AddToProtocol("Původní soubor METS přepsán.");
                    }
                }
                else
                    AddToProtocol("Nepodařilo se vygenerovat nové URNNBN.");
                
            }
        }

        #region Requests
        private async Task<XDocument> CreateRequest(Dictionary<string, string> metaDict)
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

            if (WithPredecessor)
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
        private async Task<string> Send4RegisterURNNBN(XDocument request)
        {
            try
            {
                //https://resolver-test.nkp.cz/api/v4/registrars/ex001/digitalDocuments
                string url = _serverURL + "/registrars/" + _sigla + "/digitalDocuments";

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
        private async Task<bool> Send4DeleteURNNBN(string urnnbn)
        {
            try
            {
                //https://resolver-test.nkp.cz/api/v4/urnnbn/urn:nbn:cz:ex001-0000xy

                Authorize();

                string _urnnbn = urnnbn;

                string addr = _serverURL + "/urnnbn/" + _urnnbn;

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
        private async Task<bool> Send4DeleteIdentifiers(string urnnbn)
        {
            try
            {
                //https://resolver-test.nkp.cz/api/v4/resolver/urn:nbn:cz:ex001-0000y3/registrarScopeIdentifiers

                Authorize();

                string _urnnbn = urnnbn;

                string addr = _serverURL + "/resolver/" + _urnnbn + "/registrarScopeIdentifiers";

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
        #endregion
        #region HelperFcs
        private string GetFullName(XElement _xEle)
        {
            if (_xEle != null)
            {

                XNamespace mNS = XNamespace.Get("http://www.loc.gov/mods/v3");

                var primaryOriginatorSureName = _xEle.Descendants(mNS + "namePart").Where(x => x.Attribute("type").Value == "family").FirstOrDefault(); //ověřit
                var primaryOriginatorName = _xEle.Descendants(mNS + "namePart").Where(x => x.Attribute("type").Value == "given").FirstOrDefault(); //ověřit

                string primaryOriginatorFullName = "";
                if (primaryOriginatorSureName != null)
                {
                    primaryOriginatorFullName = primaryOriginatorSureName.Value + " " + primaryOriginatorName.Value;
                }

                return primaryOriginatorFullName;
            }
            else return null;
        }
        private void Authorize()
        {
            var byteArray = Encoding.ASCII.GetBytes(_login + ":" + _psw);
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        }
        private void AddToProtocol(string protocolText ,string responseText = null, string ifStartWith = null,string elseText = null )
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

        #endregion
    }
}
