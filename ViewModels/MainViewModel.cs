
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using URNNBNSolver.Shared;


namespace URNNBNSolver.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
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
        private bool newUuid;
        public bool NewUuid
        {
            get
            {
                return newUuid;
            }
            set
            {
                newUuid = value;
                OnPropertyChanged();
            }
        }
        public RelayCommand SelectMetsCommand { get; set; }
        public RelayCommand GenerateCommand { get; set; }

        public MainViewModel()
        {
            SelectMetsCommand = new RelayCommand(param => this.OnSelectMetsCommand(), param => true);
            GenerateCommand = new RelayCommand(param => this.OnGenerate(), param => true);
        }
        private void OnGenerate()
        {
            //MessageBox.Show("pre=" + WithPredecessor.ToString() + "|uuid=" + NewUuid.ToString());

            if (_fileNames == null)
            {
                MessageBox.Show("Nejsou vybraná žádná mets!");
                return;
            }

            for (int i = 0; i < _fileNames.Length; i++)
            {
                XDocument _xMets = XDocument.Load(_fileNames[i]);

                Dictionary<string,string> metaDict = GetMeta(_xMets);

                MonoReq(metaDict);
            }
        }
        private XDocument MonoReq(Dictionary<string, string> metaDict)
        {
            XNamespace mNS = XNamespace.Get("http://resolver.nkp.cz/v4/");
            XDocument request = new XDocument();

            XElement root =
                new XElement(mNS + "import", new XAttribute("test", "5"),
                    new XElement(mNS + "monographVolume",
                        new XElement(mNS + "titleInfo",
                            new XElement(mNS + "monographTitle", metaDict["title"]))));

            XElement monographVolume = root.Descendants(mNS + "monographVolume").First();

            if (metaDict["subTitle"] != null)
                root.Descendants(mNS + "monographTitle").First().AddAfterSelf(new XElement(mNS + "volumeTitle", metaDict["subTitle"]));

            if (metaDict["ccnb"] != null)
                monographVolume.Add(new XElement(mNS + "ccnb", metaDict["ccnb"]));
            if (metaDict["isnb"] != null)
                monographVolume.Add(new XElement(mNS + "isnb", metaDict["isnb"]));
            if (metaDict["documentType"] != null)
                monographVolume.Add(new XElement(mNS + "documentType", metaDict["documentType"]));
            if (metaDict["digitalBorn"] != null)
                monographVolume.Add(new XElement(mNS + "digitalBorn", metaDict["digitalBorn"]));
            if (metaDict["primaryOriginatorName"] != null)
                monographVolume.Add(new XElement(mNS + "primaryOriginator", metaDict["primaryOriginatorName"]));



            return request;
        }
        private XDocument PeriReq(Dictionary<string, string> metaDict)
        {
            XDocument request = new XDocument();



            return request;
        }

        
        private Dictionary<string, string> GetMeta(XDocument _xMets)
        {
            Dictionary<string, string> metas = new Dictionary<string, string>();

            XNamespace mNS = XNamespace.Get("http://www.loc.gov/mods/v3");
            XNamespace metsNS = XNamespace.Get("http://www.loc.gov/METS/");

            metas.Add("projectType", _xMets.Root.Attribute("TYPE").Value);
            metas.Add("title",_xMets.Descendants(mNS+"title").FirstOrDefault()?.Value);
            metas.Add("subTitle", _xMets.Descendants(mNS+"subTitle").FirstOrDefault()?.Value);
            metas.Add("ccnb", _xMets.Descendants(mNS + "ccnb").FirstOrDefault()?.Value); //ověřit
            metas.Add("isnb", _xMets.Descendants(mNS + "isnb").FirstOrDefault()?.Value); //ověřit
            metas.Add("documentType", _xMets.Descendants(mNS + "documentType").FirstOrDefault()?.Value); //ověřit
            metas.Add("digitalBorn", _xMets.Descendants(mNS + "digitalBorn").FirstOrDefault()?.Value); //ověřit            
            metas.Add("primaryOriginatorName", GetFullName(_xMets.Descendants(mNS + "name").Where(x=>x.Attribute("type").Value == "personal" && x.Attribute("usage") != null && x.Attribute("usage").Value == "primary").FirstOrDefault())); //ověřit
            metas.Add("otherOriginatorName", GetFullName(_xMets.Descendants(mNS + "name").Where(x => x.Attribute("type").Value == "personal" && x.Attribute("usage") != null && x.Attribute("usage").Value == "primary").FirstOrDefault())); //ověřit
            metas.Add("publisher", _xMets.Descendants(mNS + "publisher").FirstOrDefault()?.Value);
            metas.Add("place", _xMets.Descendants(mNS + "placeTerm").Where(x=>x.Attribute("type").Value == "text").FirstOrDefault()?.Value);
            metas.Add("year", _xMets.Descendants(mNS + "dateIssued").FirstOrDefault()?.Value);
            metas.Add("uuid", _xMets.Descendants(mNS + "identifier").Where(x => x.Attribute("type").Value == "uuid").FirstOrDefault()?.Value);
            metas.Add("financed", _xMets.Descendants(mNS + "financed").FirstOrDefault()?.Value); //ověřit
            metas.Add("contractNumber", _xMets.Descendants(mNS + "contractNumber").FirstOrDefault()?.Value); //ověřit

            return metas;
        }
        private void OnSelectMetsCommand()
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
                ConfirmString = "Vybráno "+_fileNames.Length+" souborů.";
            }            
        }  
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
    }
}
