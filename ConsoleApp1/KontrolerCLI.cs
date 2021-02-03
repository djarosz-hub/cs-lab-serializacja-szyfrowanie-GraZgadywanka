using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GraZaDuzoZaMalo.Model;
using static GraZaDuzoZaMalo.Model.Gra.Odpowiedz;

namespace AppGraZaDuzoZaMaloCLI
{
    public class KontrolerCLI
    {
        public const char ZNAK_ZAKONCZENIA_GRY = 'X';

        private Gra gra;
        private WidokCLI widok;
        private object locker = new object();
        private string _plik = "graMaloDuzoBinarnie.bin";
        private bool trwa = false;
        public int MinZakres { get; private set; }
        public int MaxZakres { get; private set; }

        public IReadOnlyList<Gra.Ruch> ListaRuchow => gra.ListaRuchow;
        public KontrolerCLI()
        {
            //gra = new Gra();
            widok = new WidokCLI(this);
            Thread t = new Thread(ZapisywanieGryWTle);
            t.IsBackground = true;
            t.Start();
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
                        UruchomRozgrywke(/*false*/);
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
                UruchomRozgrywke(/*true*/);
            }
        }

        public void UruchomRozgrywke(/*bool nowa*/)
        {
            widok.CzyscEkran();
            widok.KomunikatPowitalny();
            Console.WriteLine($"{gra.MinLiczbaDoOdgadniecia} - { gra.MaxLiczbaDoOdgadniecia }");
            //if(nowa)
            //    UstawZakresDoLosowania();
            //gra = new Gra(MinZakres, MaxZakres);
            //widok.KomunikatPowitalny();
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

                //Console.WriteLine( gra.Ocena(propozycja) );
                //oceń propozycję, break
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

            //if StatusGry == Przerwana wypisz poprawną odpowiedź
            //if(gra.StatusGry == Gra.Status.Poddana)
            //    Console.WriteLine(gra.liczbaDoOdgadniecia);
            //if StatusGry == Zakończona wypisz statystyki gry
            if (gra.StatusGry == Gra.Status.Zakonczona)
            {
                Console.WriteLine($"Odgadnieto: {gra.liczbaDoOdgadniecia}");
                trwa = false;
                Thread.Sleep(1000);
                UsunPoprzedniaGre();
                //widok.CzyscEkran();
                //widok.HistoriaGry();
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
            //np. zapisuje stan gry na dysku w celu późniejszego załadowania
            //albo dopisuje wynik do Top Score
            //sprząta pamięć
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
                    IFormatter formatter = new BinaryFormatter();
                    using (Stream s = new FileStream(_plik, FileMode.Create, FileAccess.Write))
                    {
                        formatter.Serialize(s, gra);
                    }
                }
            }
            catch
            {
                Console.WriteLine("Nie udalo sie zapisac gry.");
            }
        }
        private bool WczytajPoprzedniaGre(out Gra poprzedniaGra)
        {
            poprzedniaGra = null;
            if (File.Exists(_plik))
            {
                bool flag = false;
                using (Stream s = new FileStream(_plik, FileMode.Open, FileAccess.Read))
                {
                    try
                    {
                        IFormatter formatter = new BinaryFormatter();
                        poprzedniaGra = (Gra)formatter.Deserialize(s);
                        flag = true;
                    }
                    catch
                    {
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
