using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using GraZaDuzoZaMalo.Model;
using static GraZaDuzoZaMalo.Model.Gra.Odpowiedz;

namespace AppGraZaDuzoZaMaloCLI
{
    public class KontrolerCLI
    {
        public const char ZNAK_ZAKONCZENIA_GRY = 'X';

        private Gra gra;
        private WidokCLI widok;
        private string _plik = "graMaloDuzoXML.xml";
        private bool trwa = false;
        private object locker = new object();
        public int MinZakres { get; private set; } = 1;
        public int MaxZakres { get; private set; } = 100;

        public IReadOnlyList<Gra.Ruch> ListaRuchow
        {
            get
            { return gra.ListaRuchow; }
        }

        public KontrolerCLI()
        {
            widok = new WidokCLI(this);
            Thread t = new Thread(ZapisywanieGryWTle);
            t.IsBackground = true;
            t.Start();
            List<byte> kList = new List<byte>();
            for (int i = 0; i < 32; i++)
            {
                kList.Add((byte)i);
            }
            key.Key = kList.ToArray();
        }

        public void Uruchom()
        {
            widok.OpisGry();
            if (WczytajPoprzedniaGre(out Gra wczytanaGra))
            {
                //widok.WczytajPropozycje();
                if (widok.ChceszKontynuowac("Wczytac poprzednia gre? (t/n)"))
                {
                    gra = wczytanaGra;
                    if (gra.Wznow())
                        UruchomRozgrywke();
                    else
                        Console.WriteLine("Gra skonczona.");
                }
                else
                {
                    UsunPoprzedniaGre();
                }
            }
            while (widok.ChceszKontynuowac("Czy chcesz kontynuować aplikację (t/n)? "))
            {
                UstawZakresDoLosowania();
                gra = new Gra(MinZakres, MaxZakres);
                UruchomRozgrywke();
            }
        }

        public void UruchomRozgrywke()
        {
            widok.CzyscEkran();
            widok.KomunikatPowitalny();
            Console.WriteLine($"{gra.MinLiczbaDoOdgadniecia} - { gra.MaxLiczbaDoOdgadniecia }");
            trwa = true;
            do
            {
                int propozycja = 0;
                try
                {
                    propozycja = widok.WczytajPropozycje();
                }
                catch (KoniecGryException)
                {
                    gra.Przerwij();
                    ZakonczGre();
                }

                Console.WriteLine(propozycja);

                if (gra.StatusGry == Gra.Status.Poddana) break;

                switch (gra.Ocena(propozycja))
                {
                    case ZaDuzo:
                        widok.KomunikatZaDuzo();
                        break;
                    case ZaMalo:
                        widok.KomunikatZaMalo();
                        break;
                    case Trafiony:
                        widok.KomunikatTrafiono();
                        break;
                    default:
                        break;
                }
                widok.HistoriaGry();
            }
            while (gra.StatusGry == Gra.Status.WTrakcie);
            trwa = false;

            if (gra.StatusGry == Gra.Status.Zakonczona)
            {
                Console.WriteLine($"Odgadnieto: {gra.liczbaDoOdgadniecia}");
                trwa = false;
                Thread.Sleep(1000);
                UsunPoprzedniaGre();
            }
        }

        ///////////////////////

        public void UstawZakresDoLosowania()
        {
            Console.WriteLine("Podaj dolny zakres: ");
            //int min = 1;
            //int max = 100;
            bool low = int.TryParse(Console.ReadLine(), out int min);
            if (!low)
            {
                Console.WriteLine("Blad, przyjeto dolny zakres = 1");
                min = 1;
            }
            Console.WriteLine("Podaj gorny zakres: ");
            bool hi = int.TryParse(Console.ReadLine(), out int max);
            if (!hi)
            {
                Console.WriteLine("Blad, przyjeto gorny zakres = 100");
                max = 100;
            }
            MinZakres = min;
            MaxZakres = max;
        }

        public int LiczbaProb() => gra.ListaRuchow.Count();

        public void ZakonczGre()
        {
            ZapiszGre();
            gra = null;
            widok.CzyscEkran();
            Console.WriteLine("Gra zakonczona.");
            widok = null;
            System.Environment.Exit(0);
        }

        public void ZakonczRozgrywke()
        {
            gra.Przerwij();
        }
        private void ZapisywanieGryWTle()
        {
            if (trwa)
                ZapiszGre();
            Thread.Sleep(1000);
            ZapisywanieGryWTle();
        }
        private void UsunPoprzedniaGre()
        {
            File.Delete(_plik);
        }
        private void ZapiszGre()
        {
            try
            {
                lock (locker)
                {
                    DataContractSerializer dcs = new DataContractSerializer(typeof(Gra));
                    using Stream s = new FileStream(_plik, FileMode.Create, FileAccess.Write,FileShare.None);
                    using MemoryStream mS = new MemoryStream();
                    dcs.WriteObject(mS, gra);
                    mS.Position = 0;
                    var temp = ZaszyfrujPlik(mS);
                    temp.Save(s);
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                //Console.WriteLine("Nie udalo sie zapisac gry.");
            }
        }
        private Aes key = Aes.Create();
        private XmlDocument OdszyfrujPlik(Stream s)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(s);
            XmlElement zaszyfrowana = xmlDoc.GetElementsByTagName("EncryptedData")[0] as XmlElement;
            EncryptedData encData = new EncryptedData();
            encData.LoadXml(zaszyfrowana);
            EncryptedXml temp = new EncryptedXml();
            byte[] output = temp.DecryptData(encData, key);
            temp.ReplaceData(zaszyfrowana, output);
            return xmlDoc;
        }
        private XmlDocument ZaszyfrujPlik(Stream s)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(s);
            XmlElement liczbaDoOdgadniecia = xmlDoc.GetElementsByTagName("liczbaDoOdgadniecia")[0] as XmlElement;
            EncryptedXml liczbaSzyfrowana = new EncryptedXml();
            byte[] zaszyfrowana = liczbaSzyfrowana.EncryptData(liczbaDoOdgadniecia, key, false);
            EncryptedData zaszyfrowaneDane = new EncryptedData();
            zaszyfrowaneDane.Type = EncryptedXml.XmlEncElementUrl;
            zaszyfrowaneDane.EncryptionMethod = new EncryptionMethod(EncryptedXml.XmlEncAES256Url);
            zaszyfrowaneDane.CipherData.CipherValue = zaszyfrowana;
            EncryptedXml.ReplaceElement(liczbaDoOdgadniecia, zaszyfrowaneDane, false);
            return xmlDoc;
        }
        private bool WczytajPoprzedniaGre(out Gra poprzedniaGra)
        {
            poprzedniaGra = null;
            if (File.Exists(_plik))
            {
                bool flag = false;
                using (Stream s = new FileStream(_plik, FileMode.Open, FileAccess.Read, FileShare.None))
                using (MemoryStream mS = new MemoryStream())
                {
                    try
                    {
                        DataContractSerializer dcs = new DataContractSerializer(typeof(Gra));
                        var temp = OdszyfrujPlik(s);
                        temp.Save(mS);
                        mS.Position = 0;
                        gra = (Gra)dcs.ReadObject(mS);
                        flag = true;
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Console.WriteLine("Nie wczytano");
                    }
                }
                return flag;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="defaultValue"></param>
        /// <exception cref="KoniecGryException"></exception>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="OverflowException"></exception>
        /// <returns></returns>
        public int WczytajLiczbeLubKoniec(string value, int defaultValue)
        {
            widok.CzyscEkran();
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            value = value.TrimStart().ToUpper();
            if (value.Length > 0 && value[0].Equals(ZNAK_ZAKONCZENIA_GRY))
                throw new KoniecGryException();

            //UWAGA: ponizej może zostać zgłoszony wyjątek 
            return Int32.Parse(value);
        }
    }

    [Serializable]
    internal class KoniecGryException : Exception
    {
        public KoniecGryException()
        {
        }

        public KoniecGryException(string message) : base(message)
        {
        }

        public KoniecGryException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected KoniecGryException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
