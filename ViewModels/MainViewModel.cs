
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
using System.Windows.Forms;
using System.Xml.Linq;
using URNNBNSolver.Models;
using URNNBNSolver.Shared;




namespace URNNBNSolver.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        #region Properties
        
        private RequestHandler _requestHandler;
        private string[] _fileNames;

        private string logPath;
        public string LogPath
        {
            get
            {
                return logPath;
            }
            set
            {
                logPath = value;
                OnPropertyChanged();
            }
        }

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
        public int SelectedIndex { get; set; }
        public List<string> Servs { get; set; }

        string selectedServ = null;
        public string SelectedServ
        {
            get { return this.selectedServ; }
            set
            {
                if (_requestHandler != null)
                {
                    if (value.StartsWith("Test"))
                    {
                        _requestHandler.ServerURL = "https://resolver-test.nkp.cz/api/v4";
                        _requestHandler.Sigla = "ex001";
                    }
                    else
                    {
                        _requestHandler.ServerURL = "testChangeServer";
                        _requestHandler.Sigla = "ex001";
                    }
                }
                
                this.selectedServ = value;
                
                OnPropertyChanged(); 
            }
        }


       
        #endregion

        public RelayCommand SelectMetsCommand { get; set; }
        public RelayCommand GenerateCommand { get; set; }
        public RelayCommand SetLogCommand { get; set; }
        public MainViewModel()
        {
            Servs = new List<string> { "Produkční server", "Test server"};
            SelectedIndex = 1;

            _requestHandler = new RequestHandler();

            SelectMetsCommand = new RelayCommand(param => this.OnSelectMets(), param => true);
            GenerateCommand = new RelayCommand(param => this.OnGenerateURNNBN(), param => true);
            SetLogCommand = new RelayCommand(param => this.OnSetLog(), param => true);
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
            metas.Add("urnnbn", _xMets.Descendants(mNS + "identifier").Where(x => x.Attribute("type").Value == "urnnbn").FirstOrDefault()?.Value);
            metas.Add("financed", _xMets.Descendants(mNS + "financed").FirstOrDefault()?.Value); //ověřit
            metas.Add("contractNumber", _xMets.Descendants(mNS + "contractNumber").FirstOrDefault()?.Value); //ověřit

            if(metas["projectType"].ToLower().Equals("periodical"))
            {
                var issueMods = _xMets.Descendants(mNS +"mods").Where(x => x.Attribute("ID").Value.Contains("_ISSUE_")).FirstOrDefault();
                metas.Add("uuid", issueMods.Descendants(mNS + "identifier").Where(x => x.Attribute("type").Value == "uuid").FirstOrDefault()?.Value);
            }
            else
            {
                var volumeMods = _xMets.Descendants(mNS + "mods").Where(x => x.Attribute("ID").Value.Contains("_VOLUME_")).FirstOrDefault();
                metas.Add("uuid", volumeMods.Descendants(mNS + "identifier").Where(x => x.Attribute("type").Value == "uuid").FirstOrDefault()?.Value);
            }
            

            return metas;
        }
        private void OnSelectMets()
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
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
                System.Windows.MessageBox.Show("Nejsou vybraná žádná mets!");
                return;
            }

            InitProtocol();
            
            AddToProtocol("Zpracováni probíha na " + _requestHandler.ServerURL );
            AddToProtocol("");

            for (int i = 0; i < _fileNames.Length; i++)
            {
                AddToProtocol("*****************START*****************");

                AddToProtocol("[" + DateTime.Now.ToLongTimeString() + "]");
                AddToProtocol("soubor : " + Path.GetFileNameWithoutExtension(_fileNames[i]));

                XDocument _xMets = XDocument.Load(_fileNames[i]);

                Dictionary<string,string> _metaDict = GetMeta(_xMets);

                if (_metaDict["urnnbn"] == null)
                {
                    AddToProtocol("Soubor neobsahuje urnnbn.");
                    return;
                }

                XDocument _xReq = await _requestHandler.CreateRequest(_metaDict,WithPredecessor);
               
                //var urnnbn = await _requestHandler.Send4RegisterURNNBN(_xReq);

                //WriteXML(urnnbn,_xMets, _fileNames[i]);

                AddToProtocol("URNNBN : " + "create deactivated"/*urnnbn*/);

                AddToProtocol("******************END******************");

            }
        }
        private void OnSetLog()
        {

            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    LogPath = fbd.SelectedPath;
                }
            }
        }

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
        private void InitProtocol()
        {
            ProtocolCollection = new ObservableCollection<string>();
            _requestHandler.ProtocolCollection = this.ProtocolCollection;
        }
        private void AddToProtocol(string protocolText, string responseText = null, string ifStartWith = null, string elseText = null)
        {
            string toAdd = "";

            if (ifStartWith == null)
                toAdd = protocolText;
            else
            {
                if (responseText.StartsWith(ifStartWith))
                    toAdd = elseText;
                else
                    toAdd = protocolText;
            }

            if (!toAdd.StartsWith("*") && !toAdd.StartsWith("Zpracováni") && !toAdd.StartsWith("[") && toAdd != "")
            {
                toAdd = "->" + toAdd;
            }

            ProtocolCollection.Add(toAdd);
        }
        private void WriteXML(string urnnbn,XDocument _xMets,string fileName)
        {
            if (urnnbn != null)
            {
                XNamespace mNS = XNamespace.Get("http://www.loc.gov/mods/v3");
                XNamespace dNS = XNamespace.Get("http://purl.org/dc/elements/1.1/");

                var urnNodesMods = _xMets.Descendants(mNS + "identifier").Where(x => x.Attribute("type").Value == "urnnbn");
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
                    string dir = Path.GetDirectoryName(fileName);
                    string original = dir + "/" + Path.GetFileNameWithoutExtension(fileName) + "_original.xml";

                    if (File.Exists(original))
                    {
                        File.Delete(original);
                    }

                    File.Move(fileName, original);

                    _xMets.Save(fileName);
                    AddToProtocol("Původní soubor byl zachován.");
                }
                else
                {
                    _xMets.Save(fileName);
                    AddToProtocol("Původní soubor METS přepsán.");
                }
            }
            else
                AddToProtocol("Nepodařilo se vygenerovat nové URNNBN.");
        }

        #endregion
    }
}
