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
        static long[,] mat_contextos;           //matriz de contextos
        static double ciclos_reloj;             //cantidad de ciclos de reloj
        static int quantum_total;               //valor del usuario para quantum
        static int[,] cache_datos_1;            //matriz de cache de datos 1
        static int[,] cache_datos_2;            //matriz de cache de datos 2
        static int[,] cache_datos_3;            //matriz de cache de datos 3 
        static int RL_1;
        static int RL_2;
        static int RL_3;
        static bool lento;

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
            int x = 16;
            return (direccion % (x) / 4);
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
            for (int i = 0; i < mem_principal_datos.Length; i++)
            {
                mem_principal_datos[i] = 1;
            }
            mem_principal_instruc = new int[640];
            inicializarMemorias(ref mem_principal_instruc);

            cache_datos_1 = new int[6, 4];
            cache_datos_2 = new int[6, 4];
            cache_datos_3 = new int[6, 4];


            cache_datos_1[4, 0] = -1;
            cache_datos_1[4, 1] = -1;
            cache_datos_1[4, 2] = -1;
            cache_datos_1[4, 3] = -1;
            cache_datos_2[4, 0] = -1;
            cache_datos_2[4, 1] = -1;
            cache_datos_2[4, 2] = -1;
            cache_datos_2[4, 3] = -1;
            cache_datos_3[4, 0] = -1;
            cache_datos_3[4, 1] = -1;
            cache_datos_3[4, 2] = -1;
            cache_datos_3[4, 3] = -1;

            ultimo_mem_inst = 0;
            ciclos_reloj = 0;
            cant_hilillos = 0;
            quantum_total = 0;
            file_path = "../../../Hilillos/";
            hilillos_tomados = new List<int>();
        }
        private static void inicializarMemorias(ref int[] matriz)
        {
            for (int i = 0; i < matriz.Length; i++)
            {
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
            mat_contextos[i, 32] = ultimo_viejo + 384;   //el PC

        }
        public void leer_muchos_hilillos()//permite cargar todos los hilillos desde el txt a memoria
        {
            mat_contextos = new long[cant_hilillos, 35];
            for (int i = 0; i < cant_hilillos; i++)
            {
                leer_hilillo_txt(i);
            }
            mem_principal_instruc[ultimo_mem_inst] = 1;


            for (int i = 0; i < cant_hilillos; i++)
            {
                for (int j = 0; j < 35; j++)
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

            bool hilillo_escogido = false;
            while (hilillo_escogido == false)
            {
                if (Monitor.TryEnter(mat_contextos))
                {
                    try
                    {
                        for (int i = 0; i < cant_hilillos && hilillo_escogido == false; i++)
                        {
                            if (mat_contextos[i, 33] != 1)
                            {
                                if (!hilillos_tomados.Contains(i + 1))
                                {
                                    mat_contextos[i, 34] -= long.Parse(GetTimestamp(DateTime.Now));
                                    hilillos_tomados.Add(i + 1);  //poner numero de hilillo, correspondiente con el PC
                                    hilillo_actual = i + 1;
                                    PC = (int)mat_contextos[i, 32];
                                    hilillo_escogido = true;
                                    Console.WriteLine(System.Threading.Thread.CurrentThread.Name + " tomo el hilillo " + (i));
                                }
                            }
                        }
                    }
                    finally
                    {
                        Monitor.Exit(mat_contextos);
                    }
                }
            }
        }
        private static String GetTimestamp(DateTime value)
        {
            return value.ToString("yyyyMMddhhmmssff");
        }

        static void leerInstruccion()
        {
            //Buscar en cache, instruccion
            int bloque = dir_a_bloque(PC - 384);
            if (cache_instruc[4, bloque_a_cache(bloque) * 4] == bloque)
            {
                ejecutarInstruccion(); //La instruccion estaba en cache, ejecutarla
            }
            else //subir el bloque con la instruccion a cache
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
                        try
                        {
                            accesoInstrucciones = true;
                            //subir bloque a caché
                            int acum = 0;
                            for (int ins = 0; ins < 4; ins++)
                            {
                                for (int palabr = 0; palabr < 4; palabr++, acum++)
                                {
                                    cache_instruc[ins, palabr + bloque_a_cache(bloque) * 4] = mem_principal_instruc[bloque * 16 + acum];

                                }
                            }


                            //onsole.WriteLine("memoria principal");
                            //PrintVector(mem_principal_instruc);
                            //int tem = bloque_a_cache(bloque);
                            cache_instruc[4, bloque_a_cache(bloque) * 4] = bloque;
                            //Console.WriteLine("Cache de instrucciones");
                            //PrintMatriz(cache_instruc);
                        }
                        finally
                        {
                            Monitor.Exit(mem_principal_instruc);
                        }
                        ejecutarInstruccion();
                    }
                    else
                    {
                        barreraCicloReloj.SignalAndWait(); //tratando de hacer el LOCK, se cuentan ciclos de reloj
                    }

                }
            }
        }


        static void ejecutarInstruccion()
        {
            int bloque = dir_a_bloque(PC - 384);
            bloque = bloque_a_cache(bloque);
            bloque *= 4;
            int palabra = dir_a_palabra(PC - 384) % 4;
            int[] instruccion = new int[4];
            instruccion[0] = cache_instruc[palabra, bloque];
            instruccion[1] = cache_instruc[palabra, bloque + 1];
            instruccion[2] = cache_instruc[palabra, bloque + 2];
            instruccion[3] = cache_instruc[palabra, bloque + 3];
            PC += 4;
            reDireccionarInstruccion(instruccion);
            quantum++;
            //Console.WriteLine("Quatum del nucleo: " + quantum);            
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
            registros = new int[33];
            quantum = 0;
            PC = 0;

            //PROCESO DEL NUCLEO---------------------------------------------------------------------------------------------------
            escogerHilillo();
            while (mat_contextos[hilillo_actual - 1, 33] != 1)
            {
                //Console.WriteLine(System.Threading.Thread.CurrentThread.Name + " tiene que ejecutar la instruccion en direccion " + PC);
                leerInstruccion();
                revisarSiCambioContexto();
                if (lento)
                    Console.ReadKey();

            }
            barreraCicloReloj.RemoveParticipant();
        }


        private static void finHilillo()
        {
            Console.WriteLine("Hilillo " + (hilillo_actual-1) + " paró");
            for (int i = 0; i < 32; i++)
            {
                Console.WriteLine("Registro[" + i + "]=" + registros[i]);
            }
        }


        /*-----------------------------------------------------------------------*/
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


        public static void infoFinSimulacion()
        {
            Console.WriteLine("\n**Fin de la Simulacion**\n\nLe memoria compartida quedo asi:\n");
            PrintVector(mem_principal_datos);
            Console.WriteLine("\nPara cada hilillo que corrio:\n");

            for (int i = 0; i < mat_contextos.GetLength(0); i++)
            {
                Console.Write("\n Registros: ");
                for (int j = 0; j < 32; j++)
                {
                    Console.Write(" " + mat_contextos[i, j]);
                }
                string hiloActual = System.Threading.Thread.CurrentThread.Name;
                switch (hiloActual)
                {
                    case "Nucleo1":
                        Console.Write("\nEl RL es: " + RL_1);
                        break;
                    case "Nucleo2":
                        Console.Write("\nEl RL es: " + RL_2);
                        break;
                    case "Nucleo3":
                        Console.Write("\nEl RL es: " + RL_3);
                        break;
                }



                Console.WriteLine("\nEste hilillo tardo " + mat_contextos[i, 34] + " ciclos en ejecutarse");
                Console.WriteLine("\n****Fin de Hilillo****\n");
            }
            Console.WriteLine("\n**Fin de Hilillo**\n");

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
                case 50:
                    ll_instruccion(instruc);
                    break;
                case 51:
                    sw_instruccion(instruc);
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
        {
            mat_contextos[hilillo_actual - 1, 33] = 1;
            barreraCicloReloj.SignalAndWait();
            quantum++;
        }




        /*LOAD-----------------------------------------------------------------------------------------------------------------*/
        private static void lw_instruccion(int[] instru)
        {
            int X = instru[2];
            int Y = instru[1];
            int n = instru[3];

            //Llamar proceso que hace todos los LOCKs, en la caché correspondiente
            int direccionDelDato = registros[Y] + n;

            string hiloActual = System.Threading.Thread.CurrentThread.Name;
            switch (hiloActual)
            {
                case "Nucleo1":
                    lw_nucleo(direccionDelDato, X, ref cache_datos_1, false, ref RL_1);
                    break;
                case "Nucleo2":
                    lw_nucleo(direccionDelDato, X, ref cache_datos_2, false, ref RL_2);
                    break;
                case "Nucleo3":
                    lw_nucleo(direccionDelDato, X, ref cache_datos_3, false, ref RL_3);
                    break;
            }
        }
        private static void ll_instruccion(int[] instru)
        {
            int X = instru[2];
            int Y = instru[1];
            int n = instru[3];

            //Llamar proceso que hace todos los LOCKs, en la caché correspondiente
            int direccionDelDato = registros[Y] + n;

            string hiloActual = System.Threading.Thread.CurrentThread.Name;
            switch (hiloActual)
            {
                case "Nucleo1":
                    lw_nucleo(direccionDelDato, X, ref cache_datos_1, true, ref RL_1);
                    break;
                case "Nucleo2":
                    lw_nucleo(direccionDelDato, X, ref cache_datos_2, true, ref RL_2);
                    break;
                case "Nucleo3":
                    lw_nucleo(direccionDelDato, X, ref cache_datos_3, true, ref RL_3);
                    break;
            }
        }


        private static void lw_nucleo(int direccionDelDato, int X, ref int[,] cache, bool esLoadLink, ref int RL_propio)
        {
            int bloqueDelDato = direccionDelDato / 16;
            int palabraDelDato = direccionDelDato % (16) / 4;
            int bloqueEnCache = bloque_a_cache(bloqueDelDato);

            bool fue_fallo = false;
            bool accesoDeCacheLocal = false;
            bool accesoTodas = false;

            while (accesoTodas == false)
            {
                //cacheLocal----------------------------------------------------------
                if (Monitor.TryEnter(cache))
                {
                    try
                    {
                        if (cache[4, bloque_a_cache(bloqueDelDato)] == bloqueDelDato && cache[5, bloque_a_cache(bloqueDelDato)] != 1)
                        {
                            int contenidoDeMem = cache[palabraDelDato, bloque_a_cache(bloqueDelDato)];
                            registros[X] = contenidoDeMem;
                            fue_fallo = false;
                            if (esLoadLink)
                            {
                                RL_propio = direccionDelDato;
                            }
                        } else
                        {
                            fue_fallo = true;
                        }
                        accesoDeCacheLocal = true;
                        accesoTodas = true;
                    }
                    finally
                    {
                        Monitor.Exit(cache);
                        if (accesoDeCacheLocal && fue_fallo)
                        {
                            for (int i = 0; i < 28; i++)//FALLO----------------------------
                            {
                                barreraCicloReloj.SignalAndWait();
                            }

                            //Memoria de Datos---------------------------------------------------
                            if (Monitor.TryEnter(mem_principal_datos))
                            {
                                try
                                {
                                    //Logica Instruccion-----------------------------------------
                                    if (esLoadLink)
                                    {
                                        RL_propio = direccionDelDato;
                                    }
                                    logica_lw(ref cache, bloqueDelDato, palabraDelDato, X, direccionDelDato, esLoadLink, ref RL_propio);
                                    accesoTodas = true;
                                }
                                finally
                                {
                                    Monitor.Exit(mem_principal_datos);
                                }
                            }
                            else
                            {
                                accesoTodas = false;
                                barreraCicloReloj.SignalAndWait(); //tratando de hacer el LOCK, se cuentan ciclos de reloj
                            }//----------------------------------------------------------------
                        }
                    }//-----------------------------------------------------------------------
                    
                }else
                {
                    barreraCicloReloj.SignalAndWait();
                }

            }
        }
        private static void logica_lw(ref int[,] cache, int bloqueDelDato, int palabraDelDato, int X, int direccionDelDato, bool esLoadLink, ref int RL_propio)
        {
            //subir bloque a caché
            int bloque = direccionDelDato/16*4;
            for (int ins = 0; ins < 4; ins++)
            {
                cache[ins, bloque_a_cache(direccionDelDato / 16)] = mem_principal_datos[((direccionDelDato)/4)+ins];
            }

            cache[4, bloque_a_cache(direccionDelDato / 16)] = direccionDelDato / 16;
            cache[5, bloque_a_cache(direccionDelDato / 16)] = 0;

            //Imprimir caché de datos
            PrintMatriz(cache);

            int contenidoDeMem = cache[palabraDelDato, bloque_a_cache(direccionDelDato / 16)];
            registros[X] = contenidoDeMem;
        }
        

        /*STORE-----------------------------------------------------------------------------------------------------------------*/
        private static void sw_instruccion(int[] instru)
        {//Write Through y No Write Allocate
            int X = instru[2];
            int Y = instru[1];
            int n = instru[3];

            //Llamar proceso que hace todos los LOCKs, en la caché correspondiente
            int direccionDondeSeGuarda = registros[Y] + n;

            string hiloActual = System.Threading.Thread.CurrentThread.Name;
            switch (hiloActual)
            {
                case "Nucleo1":
                    if (instru[0]==51)
                        sw_nucleo(direccionDondeSeGuarda, X, ref cache_datos_1, ref cache_datos_2, ref cache_datos_3, ref RL_1, ref RL_2, ref RL_3, true);
                    else
                        sw_nucleo(direccionDondeSeGuarda, X, ref cache_datos_1, ref cache_datos_2, ref cache_datos_3, ref RL_1, ref RL_2, ref RL_3, false);
                    break;
                case "Nucleo2":
                    if (instru[0]==51)
                        sw_nucleo(direccionDondeSeGuarda, X, ref cache_datos_2, ref cache_datos_1, ref cache_datos_3, ref RL_2, ref RL_1, ref RL_3, true);
                    else
                        sw_nucleo(direccionDondeSeGuarda, X, ref cache_datos_2, ref cache_datos_1, ref cache_datos_3, ref RL_2, ref RL_1, ref RL_3, false);
                    break;
                case "Nucleo3":
                    if (instru[0]==51)
                        sw_nucleo(direccionDondeSeGuarda, X, ref cache_datos_3, ref cache_datos_1, ref cache_datos_2, ref RL_3, ref RL_1, ref RL_2, true);
                    else
                        sw_nucleo(direccionDondeSeGuarda, X, ref cache_datos_3, ref cache_datos_1, ref cache_datos_2, ref RL_3, ref RL_1, ref RL_2, false);
                    break;
            }
        }
        private static void sw_nucleo(int direccionDondeSeGuarda, int X, ref int[,] cache, ref int[,] primeraNoLocal, ref int[,] segundaNoLocal, ref int RL_propia, ref int RL_ajena1, ref int RL_ajena2, bool esStoreConditional)
        {
            int bloqueDelDato = dir_a_bloque(direccionDondeSeGuarda);
            bool fueFallo = true;
            int j = 28;
            if (cache[4, bloque_a_cache(bloqueDelDato)] == bloqueDelDato && cache[5, bloque_a_cache(bloqueDelDato)] != 1)
            {
                j = 7;
                fueFallo = false;
            }
            for (int i = 0; i < j; i++)
            {
                barreraCicloReloj.SignalAndWait();
            }
            if (fueFallo)
                //LOCKs
                logica_sw(true, direccionDondeSeGuarda, X, ref cache, ref primeraNoLocal, ref segundaNoLocal, esStoreConditional, ref RL_propia, ref RL_ajena1, ref RL_ajena2);
            else
                //LOCKs
                logica_sw(false, direccionDondeSeGuarda, X, ref cache, ref primeraNoLocal, ref segundaNoLocal, esStoreConditional, ref RL_propia, ref RL_ajena1, ref RL_ajena2);
        }


        private static void logica_sw(bool fue_fallo, int direccionDondeSeGuarda, int X, ref int[,] cache, ref int[,] primeraNoLocal, ref int[,] segundaNoLocal, bool esStoreConditional, ref int RL_propia, ref int RL_ajena1, ref int RL_ajena2)
        {
            bool primeraCacheAgarrada = false;
            bool segundaCacheAgarrada = false;


            bool algunoNOseObtuvo = false;

            bool accesoTodas = false;
            while (accesoTodas == false)
            {
                if (Monitor.TryEnter(cache))
                {
                    try//cacheLocal-----------------------------------------------------------------------------------------
                    {
                        if (Monitor.TryEnter(mem_principal_datos))
                        {
                            try//memoria---------------------------------------------------------------------------
                            {
                                if (Monitor.TryEnter(primeraNoLocal))
                                {
                                    try//cacheAjena1---------------------------------------------------
                                    {
                                        if (RL_ajena1 == direccionDondeSeGuarda)
                                        {
                                            RL_ajena1 = -1;
                                        }
                                        int bloqueDelDato = dir_a_bloque(direccionDondeSeGuarda);
                                        if (primeraNoLocal[4, bloque_a_cache(bloqueDelDato)] == bloqueDelDato)
                                        {
                                            primeraNoLocal[5, bloque_a_cache(dir_a_bloque(direccionDondeSeGuarda))] = 1;//invalido
                                        }
                                        primeraCacheAgarrada = true;
                                    }
                                    finally
                                    {
                                        Monitor.Exit(primeraNoLocal);
                                        if (Monitor.TryEnter(segundaNoLocal))
                                        {
                                            try//cacheAjena2------------------------------------------------
                                            {
                                                if (RL_ajena2 == direccionDondeSeGuarda)
                                                {
                                                    RL_ajena2 = -1;
                                                }
                                                int bloqueDelDato = dir_a_bloque(direccionDondeSeGuarda);
                                                if (segundaNoLocal[4, bloque_a_cache(bloqueDelDato)] == bloqueDelDato)
                                                {
                                                    segundaNoLocal[5, bloque_a_cache(dir_a_bloque(direccionDondeSeGuarda))] = 1;//invalido
                                                }
                                                segundaCacheAgarrada = true;
                                            }
                                            finally
                                            {
                                                Monitor.Exit(segundaNoLocal);
                                                if (primeraCacheAgarrada && segundaCacheAgarrada)
                                                {
                                                    //se escribe en memoria
                                                    mem_principal_datos[direccionDondeSeGuarda / 4] = registros[X];
                                                }
                                            }
                                        }
                                        else
                                        {
                                            algunoNOseObtuvo = true;
                                        }//cacheAjena2--------------------------------------------------------
                                    }
                                }
                                else
                                {
                                    algunoNOseObtuvo = true;
                                    primeraCacheAgarrada = false;
                                }
                                //cacheAjena1-----------------------------------------------------------
                            }
                            finally
                            {
                                Monitor.Exit(mem_principal_datos);
                                if (esStoreConditional)
                                {
                                    if (RL_propia == direccionDondeSeGuarda)
                                    {
                                        RL_propia = -1;
                                        if (!fue_fallo)
                                        {
                                            cache[dir_a_palabra(direccionDondeSeGuarda), bloque_a_cache(direccionDondeSeGuarda / 16)] = registros[X];
                                            cache[5, bloque_a_cache(dir_a_bloque(direccionDondeSeGuarda))] = 0;//invalido
                                        }
                                    }
                                    else
                                    {
                                        registros[X] = 0;
                                    }
                                }
                                else
                                {
                                    if (!fue_fallo)
                                    {
                                        cache[dir_a_palabra(direccionDondeSeGuarda), bloque_a_cache(direccionDondeSeGuarda / 16)] = registros[X];
                                        cache[5, bloque_a_cache(dir_a_bloque(direccionDondeSeGuarda))] = 0;//invalido
                                    }
                                }//store conditional
                            }
                        }
                        else
                        {
                            algunoNOseObtuvo = true;
                        }
                        //memoria-----------------------------------------------------------------------------------
                    }
                    finally
                    {
                        Monitor.Exit(cache);
                        accesoTodas = true;
                    }
                }
                else
                {
                    if (!algunoNOseObtuvo)//tratando de hacer el LOCK
                        barreraCicloReloj.SignalAndWait(); 
                }
                //cacheLocal--------------------------------------------------------------------------------------------------------
                barreraCicloReloj.SignalAndWait(); //ciclo de reloj de la lectura
            }
        }


        /*--------------------------------------------------------------------*/
        private static void revisarSiCambioContexto()
        {
            if (quantum == quantum_total || mat_contextos[hilillo_actual - 1, 33] == 1)
            {
                if (mat_contextos[hilillo_actual - 1, 33] == 1)
                    finHilillo();
                string hiloActual = System.Threading.Thread.CurrentThread.Name;
                object locker = new object();
                switch (hiloActual)
                {
                    case "Nucleo1":
                        //RL = n + R[Y]
                        bool done = false;

                        lock (locker)
                        {
                            while (!done)
                            {
                                RL_1 = -1;
                                done = true;
                            }
                        }
                        break;
                    case "Nucleo2":
                        //RL = n + R[Y]
                        bool done1 = false;
                        lock (locker)
                        {
                            while (!done1)
                            {
                                RL_2 = -1;
                                done1 = true;
                            }
                        }
                        break;
                    case "Nucleo3":
                        //RL = n + R[Y]
                        bool done2 = false;
                        lock (locker)
                        {
                            while (!done2)
                            {
                                RL_3 = -1;
                                done2 = true;
                            }
                        }
                        break;
                }

                //hacer cambio de contexto
                quantum = 0;
                for (int i = 0; i < 32; i++)
                {
                    mat_contextos[hilillo_actual - 1, i] = registros[i];
                }
                mat_contextos[hilillo_actual - 1, 32] = PC;
                mat_contextos[hilillo_actual - 1, 34] += long.Parse(GetTimestamp(DateTime.Now));
                //escoger hilillo de nuevo
                escogerHililloNuevo();
                Console.Write("\n**Se ha realizado un cambio de contexto\n");
                // PrintMatriz(mat_contextos);
            }
        }
        static void escogerHililloNuevo()
        {
            bool hilillo_nuevo_escogido = false;
            while (hilillo_nuevo_escogido == false)
            {
                int indiceATomar = -1;
                hilillo_nuevo_escogido = true;
                for (int i = 0; i < mat_contextos.GetLength(0); i++)
                {
                    if (mat_contextos[i, 33] != 1 && !hilillos_tomados.Contains(i + 1))
                    {
                        indiceATomar = i;
                        break;
                    }
                }
                if (indiceATomar != -1)
                {
                    if (Monitor.TryEnter(mat_contextos))
                    {
                        try
                        {
                            hilillos_tomados.Remove(hilillo_actual);
                            hilillos_tomados.Add(indiceATomar + 1);  //poner numero de hilillo, correspondiente con el PC
                                                                     //mat_contextos[hilillo_actual - 1, 34] -= Int32.Parse(GetTimestamp(DateTime.Now));
                            hilillo_actual = indiceATomar + 1;
                            PC = (int)mat_contextos[indiceATomar, 32];
                            Console.WriteLine(System.Threading.Thread.CurrentThread.Name + " tomo el hilillo " + (indiceATomar));
                            for (int i = 0; i < 32; i++)
                            {
                                registros[i] = (int)mat_contextos[hilillo_actual - 1, i];
                            }
                        }
                        finally
                        {
                            Monitor.Exit(mat_contextos);
                        }
                    }
                    else
                    {
                        hilillo_nuevo_escogido = false;
                    }
                }
            }
        }




        /*-------------------------------------------------------------------*/
        /*MAIN---------------------------------------------------------------*/
        static void Main(string[] args)
        {
            Program p = new Program();
            p.menu_usuario();
            p.leer_muchos_hilillos();
            modoDeEjejcucion();
            int cantidad = cant_hilillos > 3 ? 3 : cant_hilillos;
            barreraCicloReloj = new Barrier(cantidad,
                b =>
                {
                        //Console.WriteLine("Todos han llegado.");
                        ciclos_reloj++;
                        //Console.WriteLine("Ciclos de reloj hasta ahora: {0}", ciclos_reloj);
                    });
            //crear nucleos
            var nucleo1 = new Thread(new ThreadStart(procesoDelNucelo));
            nucleo1.Name = String.Format("Nucleo{0}", 1);


            var nucleo2 = new Thread(new ThreadStart(procesoDelNucelo));
            nucleo2.Name = String.Format("Nucleo{0}", 2);

            var nucleo3 = new Thread(new ThreadStart(procesoDelNucelo));
            nucleo3.Name = String.Format("Nucleo{0}", 3);

            if (cantidad == 1)
            {
                nucleo1.Start();
            }
            else if (cantidad == 2)
            {
                nucleo1.Start();
                nucleo2.Start();
            }
            else
            {
                nucleo1.Start();
                nucleo2.Start();
                nucleo3.Start();
            }


            Console.ReadKey();
        }



        private static void PrintMatriz(int[,] matriz)
        {
            for (int i = 0; i < matriz.GetLength(0); i++)
            {
                for (int j = 0; j < matriz.GetLength(1); j++)
                {
                    Console.Write(" " + matriz[i, j]);
                }
                Console.WriteLine("");
            }
        }
        private static void PrintVector(int[] vector)
        {
            for (int i = 0; i < vector.GetLength(0); i++)
            {
                Console.Write(" " + vector[i]);
            }
            Console.WriteLine("");
        }
    }
}