using System;
using System.Collections.Generic;
using System.Threading;

namespace Proyecto_Arqui
{
    class Program
    {
        //VARIABLES GLOBALES-compartidas entre todos los nucleos
        static int[] mem_principal_datos;       //memoria principal de datos 
        static int[] mem_principal_instruc;     //memoria principal de instrucciones
        static int[,] mat_contextos;            //matriz de contextos
        static double ciclos_reloj;             //cantidad de ciclos de reloj
        static int quantum_total;               //valor del usuario para quantum
        static int[,] cache_datos_1;            //matriz de cache de datos 1
        static int[,] cache_datos_2;            //matriz de cache de datos 2
        static int[,] cache_datos_3;            //matriz de cache de datos 3 
        static bool lento;
        private static bool leer_instruccion_bool;

        //VARIABLE LOCALES-locales a cada thread
        [ThreadStatic]
        static int quantum;          //cantidad de instrucciones ejecutadas
        [ThreadStatic]
        static int[,] cache_instruc; //matriz de cache de instrucciones
        [ThreadStatic]
        static int[] registros;      //registros propios del nucleo
        [ThreadStatic]
        static int PC;               //la siguiente instruccion a ejecutar
        [ThreadStatic]
        static int hilillo_actual;	//cual hilillo se esta ejecutando en un nucleo dado

        static List<int> hilillos_tomados;
        int ultimo_mem_inst;            //'puntero' a ultimo lleno en memoria de instrucciones
        static int cant_hilillos;       //cantidad de hilillos definida por el usuario
        string file_path;               //direccion del directorio donde estaran los hilillos

        static Barrier barreraCicloReloj;

        /*Metodos direccionamiento de memoria--------------------------------*/
        private static int dir_a_bloque(int direccion)//regresa el numero de bloque
        {
            return direccion / 16;
        }
        private static int dir_a_palabra(int direccion)//regresa el numero de palabra
        {
            return (direccion % 16) / 4;
        }
        private static int bloque_a_cache(int bloque)
        {
            return bloque % 4;
        }

        /*----------------------------------------------------------------------------------------------*/
        /*Inicio del programa-----------------------------------------------*/
        public Program() //constructor, se inicializan las variables
        {
            mem_principal_datos = new int[96];
            inicializarMemorias(ref mem_principal_datos);
            mem_principal_instruc = new int[640];
            inicializarMemorias(ref mem_principal_instruc);

            cache_datos_1 = new int[6, 4];
            cache_datos_2 = new int[6, 4];
            cache_datos_3 = new int[6, 4];
            inicializarCache(ref cache_datos_1);
            inicializarCache(ref cache_datos_2);
            inicializarCache(ref cache_datos_3);

            ultimo_mem_inst = 0;
            ciclos_reloj = 0;
            cant_hilillos = 0;
            quantum_total = 0;
            file_path = "../../../Hilillos/";
            hilillos_tomados = new List<int>();
        }
        private static void inicializarCache(ref int[,] cache)
        {
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    cache[i, j] = 0;

                }
            }
        }
        private static void inicializarMemorias(ref int[] matriz) {
            for (int i = 0; i < matriz.Length; i++) {
                matriz[i] = 1;
            }
        }

        public void menu_usuario() //se manejan las preguntas iniciales al usuario
        {
            Console.WriteLine("\nCuantos hilillos?");
            cant_hilillos = Int32.Parse(Console.ReadLine());
            Console.WriteLine("\nRecuerde que el .txt de cada hilillo debe estar en la carpeta 'Hilillos'");
            Console.WriteLine("\nDe cuanto será el quantum?");
            quantum_total = Int32.Parse(Console.ReadLine());

        }


        /*Lectura txts------------------------------------------------------*/
        private void leer_hilillo_txt(int i) //Metodo auxiliar que sirve para leer un solo hilillo y meterlo en memoria
        {
            char[] delimiterChars = { ' ', '\n' };
            string text = System.IO.File.ReadAllText(@file_path + i.ToString() + ".txt");
            string[] words = text.Split(delimiterChars);
            int ultimo_viejo = ultimo_mem_inst;

            foreach (string s in words)
            {
                mem_principal_instruc[ultimo_mem_inst++] = Int32.Parse(s);
            }
            mat_contextos[i - 1, 32] = ultimo_viejo;   //el PC

        }
        public void leer_muchos_hilillos()//permite cargar todos los hilillos desde el txt a memoria
        {
            mat_contextos = new int[cant_hilillos, 35];
            for (int i = 1; i <= cant_hilillos; i++)
            {
                leer_hilillo_txt(i);
            }
            mem_principal_instruc[ultimo_mem_inst] = 1;


            for (int i = 0; i < cant_hilillos; i++)
            {
                for (int j = 0; j < 34; j++)
                {
                    if (j != 32)
                    {
                        mat_contextos[i, j] = 0;
                    }
                }
            }
        }


        /*Hilos----------------------------------------------------------*/
        static void escogerHilillo()
        {
            bool lockWasTaken = false;
            var temp = mat_contextos;
            try
            {
                Monitor.Enter(temp, ref lockWasTaken);
                bool hilillo_escogido = false;
                for (int i = 0; i < cant_hilillos && hilillo_escogido == false; i++)
                {
                    if (mat_contextos[i, 33] != 1)
                    {
                        if (!hilillos_tomados.Contains(i + 1))
                        {
                            hilillos_tomados.Add(i + 1);  //poner numero de hilillo, correspondiente con el PC
                            hilillo_actual = i + 1;
                            PC = mat_contextos[i, 32];
                            hilillo_escogido = true;
                            Console.WriteLine(System.Threading.Thread.CurrentThread.Name + " tomo el hilillo " + (i + 1));
                        }
                    }
                }
            }
            finally
            {
                if (lockWasTaken)
                {
                    Monitor.Exit(temp);
                }
            }
        }

        static void leerInstruccion()
        {
            //Buscar en cache, instruccion
            int bloque = dir_a_bloque(PC);
            if (cache_instruc[4, bloque_a_cache(bloque) * 4] == bloque)
            {
                ejecutarInstruccion();
            }
            else
            {
                for (int i = 0; i < 28; i++)
                {
                    barreraCicloReloj.SignalAndWait();
                }

                //LOCK de la memoria de instrucciones, principal
                bool accesoInstrucciones = false;
                while (accesoInstrucciones == false)
                {
                    if (Monitor.TryEnter(mem_principal_instruc))
                    {
                        accesoInstrucciones = true;
                        //subir bloque a caché
                        int acum = 0;
                        for (int i = 0; i < 4; i++)
                        {
                            for (int j = 0; j < 4; j++, acum++)
                            {
                                cache_instruc[i, j + bloque_a_cache(bloque) * 4] = mem_principal_instruc[PC + acum];
                            }
                        }

                        int tem = bloque_a_cache(bloque);
                        cache_instruc[4, bloque_a_cache(bloque) * 4] = bloque;

                        //Imprimir caché de instrucciones
                        for (int i = 0; i < 5; i++)
                        {
                            for (int j = 0; j < 16; j++)
                            {
                                Console.Write(cache_instruc[i, j] + "  ");
                            }
                            Console.Write("\n\n");
                        }

                        ejecutarInstruccion();
                    }
                    else
                        barreraCicloReloj.SignalAndWait(); //tratando de hacer el LOCK, se cuentan ciclos de reloj
                }
            }
        }


        static void ejecutarInstruccion()
        {
            int bloque = dir_a_bloque(PC);
            bloque = bloque_a_cache(bloque);
            bloque *= 4;
            int palabra = dir_a_palabra(PC);
            int[] instruccion = new int[4];
            instruccion[0] = cache_instruc[palabra, bloque];
            instruccion[1] = cache_instruc[palabra, bloque + 1];
            instruccion[2] = cache_instruc[palabra, bloque + 2];
            instruccion[3] = cache_instruc[palabra, bloque + 3];
            reDireccionarInstruccion(instruccion);
            int k = 0;
            //poner barreras para el paso de instrucciones y manejar el reloj global

            //PC+=4
        }
        private static void procesoDelNucelo()
        {
            //INICIALIZACION
            cache_instruc = new int[5, 16];
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    if (i == 4)
                    {
                        cache_instruc[i, j] = -1;
                    }
                    else
                    {
                        cache_instruc[i, j] = 0;
                    }
                }
            }
            registros = new int[34];
            quantum = 0;
            PC = 0;

            //PROCESO DEL NUCLEO---------------------------------------------------------------------------------------------------
            escogerHilillo();
            Console.WriteLine(System.Threading.Thread.CurrentThread.Name + " tiene que ejecutar la instruccion en direccion " + PC);
            leerInstruccion();



            //Hacer cambio de contexto, cuando se termina una instrucción
            //Tomar en cuenta el quantum local
        }


        private static void modoDeEjejcucion()
        {
            bool failed = true;
            while (failed)
            {
                Console.WriteLine("\nDesea que el modo de ejecución sea Lento(1) o Rápido(2)");
                int parsed;
                if (Int32.TryParse(Console.ReadLine(), out parsed))
                {
                    switch (parsed)
                    {
                        case 1:
                            lento = true;
                            failed = false;
                            break;
                        case 2:
                            lento = false;
                            failed = false;
                            break;
                        default:
                            Console.WriteLine("\nRespuesta en el formato incorrecto, sólo puede contestar 1 para Lento o 2 para Rápido");
                            break;
                    }
                }
            }

        }

        /*-------------------------------------------------------------------*/
        /*MAIN---------------------------------------------------------------*/
        static void Main(string[] args)
        {
            barreraCicloReloj = new Barrier(3,
                b =>
                { // This method is only called when all the paricipants arrived.
                    //Console.WriteLine("Todos han llegado.");
                    ciclos_reloj++;
                    //Console.WriteLine("Ciclos de reloj hasta ahora: {0}", ciclos_reloj);
                });
            Program p = new Program();
            p.menu_usuario();
            p.leer_muchos_hilillos();
            modoDeEjejcucion();
            //IMPRESION DE MEMORIA INSTRUCCIONES
            for (int i = 0; i < mem_principal_instruc.Length; i++)
            {
                if (mem_principal_instruc[i] != 1)
                {
                    Console.Write(mem_principal_instruc[i] + "  ");
                    if (i != 0 && (i + 1) % 4 == 0)
                    {
                        Console.WriteLine("\n");
                    }
                }
            }
            /*//IMPRESION DE MATRIZ DE CONTEXTOS
            for (int i = 0; i < cant_hilillos; i++)
            {
                Console.Write(mat_contextos[i, 32]+" ");
            }*/

            //crear nucleos
            var nucleo1 = new Thread(new ThreadStart(procesoDelNucelo));
            nucleo1.Name = String.Format("Nucleo{0}", 1);
            nucleo1.Start();

            var nucleo2 = new Thread(new ThreadStart(procesoDelNucelo));
            nucleo2.Name = String.Format("Nucleo{0}", 2);
            nucleo2.Start();

            var nucleo3 = new Thread(new ThreadStart(procesoDelNucelo));
            nucleo3.Name = String.Format("Nucleo{0}", 3);
            nucleo3.Start();

            nucleo1.Join();
            nucleo2.Join();
            nucleo3.Join();

            Console.Write(" Hilillos tomados: ");
            for (int i = 0; i < hilillos_tomados.Count; i++)
            {
                Console.Write(hilillos_tomados[i] + " ");
            }

            Console.ReadKey();
        }

        /*Instrucciones----------------------------------------------------------*/
        public static void reDireccionarInstruccion(int[] instruc)
        {

            int operacion = instruc[0];
            switch (operacion)
            {
                case 8:
                    daddi_instruccion(instruc);
                    break;
                case 32:
                    dadd_instruccion(instruc);
                    break;
                case 34:
                    dsub_instruccion(instruc);
                    break;
                case 12:
                    dmul_instruccion(instruc);
                    break;
                case 14:
                    ddiv_instruccion(instruc);
                    break;
                case 4:
                    beqz_instruccion(instruc);
                    break;
                case 5:
                    bnez_instruccion(instruc);
                    break;
                case 3:
                    jal_instruccion(instruc);
                    break;
                case 2:
                    jr_instruccion(instruc);
                    break;
                case 35:
                    lw_instruccion(instruc);
                    break;
                case 43:
                    sw_instruccion(instruc);
                    break;
                case 63:
                    fin_instruccion(instruc);
                    break;
            }
        }

        private static void daddi_instruccion(int[] instru)
        {
            int param_1 = registros[instru[1]];
            int param_3 = instru[3];

            registros[instru[2]] = param_1 + param_3;
            barreraCicloReloj.SignalAndWait();
        }

        private static void dadd_instruccion(int[] instru)
        {
            int param_1 = registros[instru[1]];
            int param_2 = registros[instru[2]];

            registros[instru[3]] = param_1 + param_2;
            barreraCicloReloj.SignalAndWait();
        }
        private static void dsub_instruccion(int[] instru)
        {
            int param_1 = registros[instru[1]];
            int param_2 = registros[instru[2]];

            registros[instru[3]] = param_1 - param_2;
            barreraCicloReloj.SignalAndWait();
        }
        private static void dmul_instruccion(int[] instru)
        {
            int param_1 = registros[instru[1]];
            int param_2 = registros[instru[2]];

            registros[instru[3]] = param_1 * param_2;
            barreraCicloReloj.SignalAndWait();
        }
        private static void ddiv_instruccion(int[] instru)
        {
            int param_1 = registros[instru[1]];
            int param_2 = registros[instru[2]];

            registros[instru[3]] = param_1 / param_2;
            barreraCicloReloj.SignalAndWait();
        }
        private static void beqz_instruccion(int[] instru)
        {
            int param_1 = registros[instru[1]];
            int param_2 = instru[2];
            int param_3 = instru[3];

            if (param_1 == param_2)
            {
                PC += param_3 * 4;
            }
            barreraCicloReloj.SignalAndWait();
        }

        private static void bnez_instruccion(int[] instru)
        {
            int param_1 = registros[instru[1]];
            int param_2 = instru[2];
            int param_3 = instru[3];

            if (param_1 != param_2)
            {
                PC += param_3 * 4;
            }
            barreraCicloReloj.SignalAndWait();
        }
        private static void jal_instruccion(int[] instru)
        {
            registros[31] = PC;
            PC += instru[3];
            barreraCicloReloj.SignalAndWait();
        }
        private static void jr_instruccion(int[] instru)
        {
            PC = registros[instru[1]];
            barreraCicloReloj.SignalAndWait();
        }

        private static void fin_instruccion(int[] instru)
        //poner en matriz de contextos un finalizado
        {
            mat_contextos[hilillo_actual, 34] = 1;
            barreraCicloReloj.SignalAndWait();
        }
        private static void lw_instruccion(int[] instru)
        {
            int X = instru[2];
            int Y = instru[1];
            int n = instru[3];

            //Buscar en cache, instruccion
            int direccionDelDato = registros[Y] + n;

            string hiloActual = System.Threading.Thread.CurrentThread.Name;
            switch (hiloActual)
            {
                case "Nucleo1":
                    lw_nucleo(direccionDelDato, X, ref cache_datos_1);
                    break;
                case "Nucleo2":
                    lw_nucleo(direccionDelDato, X, ref cache_datos_2);
                    break;
                case "Nucleo3":
                    lw_nucleo(direccionDelDato, X, ref cache_datos_3);
                    break;
            }

            //Mem=lo cargado de Memoria[registros[Y]+n]
            //registros[X] = Mem;
        }
        private static void lw_nucleo(int direccionDelDato, int X, ref int[,] cache)
        {
            int bloqueDelDato = dir_a_bloque(direccionDelDato);
            int palabraDelDato = dir_a_palabra(direccionDelDato);


            if (cache_instruc[4, bloque_a_cache(bloqueDelDato) * 4] == bloqueDelDato)
            {
                int contenidoDeMem = cache[palabraDelDato, bloqueDelDato];
                registros[X] = contenidoDeMem;
            }
            else
            {
                for (int i = 0; i < 28; i++)
                {
                    barreraCicloReloj.SignalAndWait();
                }

                //LOCK de la memoria de instrucciones, principal
                bool accesoDeCacheLocal = false;
                while (accesoDeCacheLocal == false)
                {
                    //cacheLocal----------------------------------------------------------
                    bool lockWasTaken1 = false;
                    var temp1 = cache;
                    try
                    {
                        Monitor.Enter(temp1, ref lockWasTaken1);

                        //memoriaDatos---------------------------------------------------
                        bool lockWasTakenMem = false;
                        var tempMem = mem_principal_datos;
                        try
                        {
                            Monitor.Enter(tempMem, ref lockWasTakenMem);
                            //Logica Instruccion-----------------------------------------
                            logica_lw(ref cache, bloqueDelDato, palabraDelDato, X);
                            accesoDeCacheLocal = true;
                        }
                        finally
                        {
                            if (lockWasTakenMem)
                            {
                                Monitor.Exit(tempMem);
                            }
                        }
                    }
                    finally
                    {
                        if (lockWasTaken1)
                        {
                            Monitor.Exit(temp1);
                        }
                    }
                    barreraCicloReloj.SignalAndWait(); //tratando de hacer el LOCK, se cuentan ciclos de reloj
                }
            }
        }
        private static void logica_lw(ref int[,] cache, int bloqueDelDato, int palabraDelDato, int X)
        {
            //subir bloque a caché
            for (int i = 0; i < 4; i++)
            {
                cache[i, bloque_a_cache(bloqueDelDato) * 4] = mem_principal_instruc[PC];
            }
            cache_instruc[4, bloque_a_cache(bloqueDelDato) * 4] = bloqueDelDato;

            //Imprimir caché de datos
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    Console.Write(cache[i, j] + "  ");
                }
                Console.Write("\n\n");
            }

            int contenidoDeMem = cache[palabraDelDato, bloqueDelDato];
            registros[X] = contenidoDeMem;
        }

        
        private void sw_instruccion(int[] instru) {//Write Through y No Write Allocate
            int X = instru[2];
            int Y = instru[1];
            int n = instru[3];

            //Buscar en cache, instruccion
            int direccionDondeSeGuarda = registros[Y] + n;

            string hiloActual = System.Threading.Thread.CurrentThread.Name;
            switch (hiloActual)
            {
                case "Nucleo1":
                    sw_nucleo(direccionDondeSeGuarda, X, ref cache_datos_1, ref cache_datos_2, ref cache_datos_3);
                    break;
                case "Nucleo2":
                    sw_nucleo(direccionDondeSeGuarda, X, ref cache_datos_2, ref cache_datos_1, ref cache_datos_3);
                    break;
                case "Nucleo3":
                    sw_nucleo(direccionDondeSeGuarda, X, ref cache_datos_3, ref cache_datos_1, ref cache_datos_2);
                    break;
            }
        }
        private void sw_nucleo(int direccionDondeSeGuarda, int X, ref int[,] cache, ref int[,] primeraNoLocal, ref int[,] segundaNoLocal) {
            int bloqueDelDato = dir_a_bloque(direccionDondeSeGuarda);
            
            if (cache_instruc[4, bloque_a_cache(bloqueDelDato) * 4] == bloqueDelDato)
            {
                for (int i = 0; i < 7; i++)
                {
                    barreraCicloReloj.SignalAndWait();
                }
                //LOCKs
                todosLosLocks(false,direccionDondeSeGuarda,X, ref cache, ref primeraNoLocal, ref segundaNoLocal);
            }
            else
            {
                for (int i = 0; i < 28; i++)
                {
                    barreraCicloReloj.SignalAndWait();
                }
                //LOCKs
                todosLosLocks(true, direccionDondeSeGuarda, X, ref cache, ref primeraNoLocal, ref segundaNoLocal);

            }
        }
        private static void todosLosLocks(bool fue_fallo, int direccionDondeSeGuarda, int X, ref int[,] cache, ref int[,] primeraNoLocal, ref int[,] segundaNoLocal) {
            bool accesoTodas = false;
            while (accesoTodas == false)
            {
                bool lockLocal = false;
                var temp1 = cache;
                try//cacheLocal
                {
                    Monitor.Enter(temp1, ref lockLocal);

                    bool lockMem = false;
                    var tempMem = mem_principal_datos;
                    try//memoriaDatos
                    {
                        Monitor.Enter(tempMem, ref lockMem);
                        //CACHES NO LOCALES

                        bool lockCache2 = false;
                        var tempCache2 = primeraNoLocal;
                        try//cache2--------------------------------------------------------------------------
                        {
                            Monitor.Enter(tempCache2, ref lockCache2);

                            bool lockCache3 = false;
                            var tempCache3 = segundaNoLocal;
                            try//cache3------------------------------------------------------------
                            {
                                Monitor.Enter(tempCache3, ref lockCache3);
                                //LOGICA
                                if (fue_fallo)
                                {
                                    //se escribe solo en memoria
                                    mem_principal_datos[direccionDondeSeGuarda]=registros[X];
                                }
                                else {
                                    //se escribe en cache y en memoria
                                    cache[dir_a_palabra(direccionDondeSeGuarda), bloque_a_cache(dir_a_bloque(direccionDondeSeGuarda))] = registros[X];
                                    mem_principal_datos[direccionDondeSeGuarda] = registros[X];
                                }
                                accesoTodas = true;
                            }
                            finally
                            {
                                if (lockCache3)
                                {
                                    Monitor.Exit(tempCache3);
                                }
                            }//cache3--------------------------------------------------------------
                        }
                        finally//cache2
                        {
                            if (lockCache2)
                            {
                                Monitor.Exit(tempCache2);
                            }
                        }//cache2-----------------------------------------------------------------------------

                    }
                    finally//memoriaDatos
                    {
                        if (lockMem)
                        {
                            Monitor.Exit(tempMem);
                        }
                    }
                }
                finally//cacheLocal
                {
                    if (lockLocal)
                    {
                        Monitor.Exit(temp1);
                    }
                }
                barreraCicloReloj.SignalAndWait(); //tratando de hacer el LOCK, se cuentan ciclos de reloj
            }
        }

    }
}

/*links
 http://stackoverflow.com/questions/4190949/create-multiple-threads-and-wait-all-of-them-to-complete
 https://www.dotnetperls.com/interlocked
 https://msdn.microsoft.com/en-us/library/system.threading.interlocked.aspx
 http://stackoverflow.com/questions/6029804/how-does-lock-work-exactly
 http://stackoverflow.com/questions/24975239/barrier-class-c-sharp
 https://msdn.microsoft.com/en-us/library/dd537615(v=vs.110).aspx
 http://geekswithblogs.net/jolson/archive/2009/02/09/an-intro-to-barrier.aspx
 http://www.codeproject.com/Articles/667298/Using-ThreadStaticAttribute
*/
