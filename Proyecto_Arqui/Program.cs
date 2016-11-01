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
            mem_principal_datos = new int[384];
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
            mat_contextos[i, 32] = ultimo_viejo+384;   //el PC

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
                                    Console.WriteLine(System.Threading.Thread.CurrentThread.Name + " tomo el hilillo " + (i + 1));
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
            int bloque = dir_a_bloque(PC-384);
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
                            for (int ins=0; ins < 4; ins++)
                            {
                                for (int palabr=0; palabr<4; palabr++, acum++)
                                {
                                    cache_instruc[ins, palabr + bloque_a_cache(bloque) * 4]=mem_principal_instruc[bloque*16+acum];

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
            int bloque = dir_a_bloque(PC-384);
            bloque = bloque_a_cache(bloque);
            bloque *= 4;
            int palabra = dir_a_palabra(PC-384) % 4;
            int[] instruccion = new int[4];
            instruccion[0] = cache_instruc[palabra, bloque];
            instruccion[1] = cache_instruc[palabra, bloque + 1];
            instruccion[2] = cache_instruc[palabra, bloque + 2];
            instruccion[3] = cache_instruc[palabra, bloque + 3];
            PC += 4;
            reDireccionarInstruccion(instruccion);
            quantum++;
            Console.WriteLine("Quatum del nucleo: " + quantum);            
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
                Console.WriteLine(System.Threading.Thread.CurrentThread.Name +
                                  " tiene que ejecutar la instruccion en direccion " + PC);
                leerInstruccion();
                revisarSiCambioContexto();
                if (lento)
                    Console.ReadKey();
            }
            Console.WriteLine(System.Threading.Thread.CurrentThread.Name +"paro");
            for(int i=0; i<32;i++)
            {
                Console.WriteLine("Registro["+i+"]="+registros[i]);
            }

            barreraCicloReloj.RemoveParticipant();
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
                    sc_instruccion(instruc);
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
                    lw_nucleo(direccionDelDato, X, ref cache_datos_1, false, hiloActual);
                    break;
                case "Nucleo2":
                    lw_nucleo(direccionDelDato, X, ref cache_datos_2, false, hiloActual);
                    break;
                case "Nucleo3":
                    lw_nucleo(direccionDelDato, X, ref cache_datos_3, false, hiloActual);
                    break;
            }
        }
        private static void lw_nucleo(int direccionDelDato, int X, ref int[,] cache, bool esLoadLink, string hiloActual)
        {
            int bloqueDelDato = dir_a_bloque(direccionDelDato);
            int palabraDelDato = dir_a_palabra(direccionDelDato);


            if (cache[4, bloque_a_cache(bloqueDelDato)] == bloqueDelDato || cache[5, bloque_a_cache(bloqueDelDato)] == 1)
            {
                int contenidoDeMem = cache[palabraDelDato, bloque_a_cache(bloqueDelDato)];
                registros[X] = contenidoDeMem;
            }
            else
            {
                for (int i = 0; i < 28; i++)
                {
                    barreraCicloReloj.SignalAndWait();
                }

                //LOCKs
                bool pasoLockDeAdentro = false;

                bool accesoDeCacheLocal = false;
                while (accesoDeCacheLocal == false)
                {
                    pasoLockDeAdentro = false;
                    //cacheLocal----------------------------------------------------------
                    if (Monitor.TryEnter(cache))
                    {
                        try
                        {
                            //Memoria de Datos---------------------------------------------------
                            if (Monitor.TryEnter(mem_principal_datos))
                            {
                                try
                                {
                                    //Logica Instruccion-----------------------------------------
                                    logica_lw(ref cache, bloqueDelDato, palabraDelDato, X, direccionDelDato, esLoadLink, hiloActual);
                                    accesoDeCacheLocal = true;
                                    pasoLockDeAdentro = true;
                                }
                                finally
                                {
                                    Monitor.Exit(mem_principal_datos);
                                }
                            }
                            else
                            {
                                barreraCicloReloj.SignalAndWait(); //tratando de hacer el LOCK, se cuentan ciclos de reloj
                            }
                        }
                        finally
                        {
                            Monitor.Exit(cache);
                        }
                    }
                    else
                    {
                        if (!pasoLockDeAdentro)
                            barreraCicloReloj.SignalAndWait(); //tratando de hacer el LOCK, se cuentan ciclos de reloj
                    }

                }
            }
        }
        private static void logica_lw(ref int[,] cache, int bloqueDelDato, int palabraDelDato, int X, int direccionDelDato, bool esLoadLink, string hiloActual)
        {
            //subir bloque a caché
            for (int i = 0; i < 4; i++)
            {
                cache[i, bloque_a_cache(bloqueDelDato)] = mem_principal_datos[direccionDelDato/4];
            }
            cache[4, bloque_a_cache(bloqueDelDato)] = bloqueDelDato;

            //Imprimir caché de datos
            PrintMatriz(cache);

            int contenidoDeMem = cache[palabraDelDato, bloque_a_cache(bloqueDelDato)];
            registros[X] = contenidoDeMem;
            if (esLoadLink)
            {
                switch (hiloActual)
                {
                    case "Nucleo1":
                        bool obtenidoLock = false;
                        while (obtenidoLock == false)
                        {
                            if (Monitor.TryEnter(RL_1))
                            {
                                try
                                {
                                    RL_1 = direccionDelDato;
                                    obtenidoLock = true;
                                }
                                finally
                                {
                                    Monitor.Exit(RL_1);
                                }
                            }
                        }
                        break;
                    case "Nucleo2":
                        bool obtenidoLock1 = false;
                        while (obtenidoLock1 == false)
                        {
                            if (Monitor.TryEnter(RL_2))
                            {
                                try
                                {
                                    RL_2 = direccionDelDato;
                                    obtenidoLock1 = true;
                                }
                                finally
                                {
                                    Monitor.Exit(RL_2);
                                }
                            }
                        }
                        break;
                    case "Nucleo3":
                        bool obtenidoLock2 = false;
                        while (obtenidoLock2 == false)
                        {
                            if (Monitor.TryEnter(RL_3))
                            {
                                try
                                {
                                    RL_3 = direccionDelDato;
                                    obtenidoLock2 = true;
                                }
                                finally
                                {
                                    Monitor.Exit(RL_3);
                                }
                            }
                        }
                        break;
                }
                //RL = n + R[Y]
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
                    lw_nucleo(direccionDelDato, X, ref cache_datos_1, true, hiloActual);
                    break;
                case "Nucleo2":
                    lw_nucleo(direccionDelDato, X, ref cache_datos_2, true, hiloActual);
                    break;
                case "Nucleo3":
                    lw_nucleo(direccionDelDato, X, ref cache_datos_3, true, hiloActual);
                    break;
            }
        }




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
                    sw_nucleo(direccionDondeSeGuarda, X, ref cache_datos_1, ref cache_datos_2, ref cache_datos_3,false);
                    break;
                case "Nucleo2":
                    sw_nucleo(direccionDondeSeGuarda, X, ref cache_datos_2, ref cache_datos_1, ref cache_datos_3, false);
                    break;
                case "Nucleo3":
                    sw_nucleo(direccionDondeSeGuarda, X, ref cache_datos_3, ref cache_datos_1, ref cache_datos_2, false);
                    break;
            }
        }
        private static void sw_nucleo(int direccionDondeSeGuarda, int X, ref int[,] cache, ref int[,] primeraNoLocal, ref int[,] segundaNoLocal, bool esStoreConditional)
        {
            int bloqueDelDato = dir_a_bloque(direccionDondeSeGuarda);

            if (cache_instruc[4, bloque_a_cache(bloqueDelDato) * 4] == bloqueDelDato)
            {
                for (int i = 0; i < 7; i++)
                {
                    barreraCicloReloj.SignalAndWait();
                }
                //LOCKs
                todosLosLocks(false, direccionDondeSeGuarda, X, ref cache, ref primeraNoLocal, ref segundaNoLocal, esStoreConditional);
            }
            else
            {
                for (int i = 0; i < 28; i++)
                {
                    barreraCicloReloj.SignalAndWait();
                }
                //LOCKs
                todosLosLocks(true, direccionDondeSeGuarda, X, ref cache, ref primeraNoLocal, ref segundaNoLocal, esStoreConditional);

            }
        }
        private static void todosLosLocks(bool fue_fallo, int direccionDondeSeGuarda, int X, ref int[,] cache, ref int[,] primeraNoLocal, ref int[,] segundaNoLocal, bool esStoreConditional)
        {
            bool algunoNOseObtuvo = false;

            bool accesoTodas = false;
            while (accesoTodas == false)
            {
                algunoNOseObtuvo = false;
                //cacheLocal
                if (Monitor.TryEnter(cache))
                {
                    try
                    {
                        //memoriaDatos
                        if (Monitor.TryEnter(mem_principal_datos))
                        {
                            try
                            {
                                //cache2--------------------------------------------------------------------------
                                if (Monitor.TryEnter(primeraNoLocal))
                                {
                                    try
                                    {
                                        primeraNoLocal[5, bloque_a_cache(dir_a_bloque(direccionDondeSeGuarda))] = 1;//invalido
                                        //cache3------------------------------------------------------------
                                        if (Monitor.TryEnter(segundaNoLocal))
                                        {
                                            try
                                            {
                                                //LOGICA
                                                if (fue_fallo)
                                                {
                                                    //se escribe solo en memoria
                                                    mem_principal_datos[direccionDondeSeGuarda/4] = registros[X];
                                                    if (esStoreConditional)
                                                    {
                                                        logica_sc(direccionDondeSeGuarda);
                                                    }
                                                }
                                                else
                                                {
                                                    //se escribe en cache y en memoria
                                                    cache[dir_a_palabra(direccionDondeSeGuarda), bloque_a_cache(dir_a_bloque(direccionDondeSeGuarda))] = registros[X];
                                                    mem_principal_datos[direccionDondeSeGuarda/4] = registros[X];
                                                }
                                                segundaNoLocal[5, bloque_a_cache(dir_a_bloque(direccionDondeSeGuarda))] = 1;//invalido]
                                                accesoTodas = true;
                                            }
                                            finally
                                            {
                                                Monitor.Exit(segundaNoLocal);
                                            }
                                        }
                                        else
                                        {
                                            algunoNOseObtuvo = true;
                                        }//cache3------------------------------------------------------------
                                    }
                                    finally
                                    {
                                        Monitor.Exit(primeraNoLocal);
                                    }
                                }
                                else
                                {
                                    algunoNOseObtuvo = true;
                                }//cache2--------------------------------------------------------------------------
                            }
                            finally
                            {
                                Monitor.Exit(mem_principal_datos);
                            }
                        }//memoriaDatos
                        else
                        {
                            algunoNOseObtuvo = true;
                        }
                    }
                    finally
                    {
                        Monitor.Exit(cache);
                    }
                }//cacheLocal
                else
                {
                    if (!algunoNOseObtuvo)
                        barreraCicloReloj.SignalAndWait(); //tratando de hacer el LOCK, se cuentan ciclos de reloj
                }
                barreraCicloReloj.SignalAndWait(); //tratando de hacer el LOCK, se cuentan ciclos de reloj
            }
        }

        
        private static void sc_instruccion(int[] instru)
        {
            int X = instru[2];
            int Y = instru[1];
            int n = instru[3];

            //Llamar proceso que hace todos los LOCKs, en la caché correspondiente
            int direccionDondeSeGuarda = registros[Y] + n;
            
            string hiloActual = System.Threading.Thread.CurrentThread.Name;
            switch (hiloActual)
            {
                case "Nucleo1":
                    if (RL_1 == direccionDondeSeGuarda)
                    {
                        sw_nucleo(direccionDondeSeGuarda, X, ref cache_datos_1, ref cache_datos_2, ref cache_datos_3, true);
                    } else
                    {
                        //Si RL no es igual a n + R[Y], se pone R[X]=0
                        registros[X] = 0;
                    }
                    break;
                case "Nucleo2":
                    if (RL_2 == direccionDondeSeGuarda)
                    {
                        sw_nucleo(direccionDondeSeGuarda, X, ref cache_datos_2, ref cache_datos_1, ref cache_datos_3, true);
                    }
                    else
                    {
                        //Si RL no es igual a n + R[Y], se pone R[X]=0
                        registros[X] = 0;
                    }
                    break;
                case "Nucleo3":
                    if (RL_3 == direccionDondeSeGuarda)
                    {
                        sw_nucleo(direccionDondeSeGuarda, X, ref cache_datos_3, ref cache_datos_1, ref cache_datos_2, true);
                    }
                    else
                    {
                        //Si RL no es igual a n + R[Y], se pone R[X]=0
                        registros[X] = 0;
                    }
                    break;
            }
        }
        private static void logica_sc(int direccionDondeSeGuarda) {
            // Se pone -1 en otras RL's, SI RL ES IGUAL A n+R[Y]
            string hiloActual = System.Threading.Thread.CurrentThread.Name;
            switch (hiloActual)
            {
                case "Nucleo1":
                    //poner RL_2 y RL_3 en -1
                    bool locksObtenidos = false;
                    while (locksObtenidos == false)
                    {
                        if (Monitor.TryEnter(RL_2))
                        {
                            try
                            {
                                if (Monitor.TryEnter(RL_3))
                                {
                                    try
                                    {
                                        RL_2 = -1;
                                        RL_3 = -1;
                                    }
                                    finally
                                    {
                                        Monitor.Exit(RL_3);
                                    }
                                }
                            }
                            finally
                            {
                                Monitor.Exit(RL_2);
                            }
                        }
                    }
                    break;
                case "Nucleo2":
                    //poner RL_1 y RL_3 en -1
                    bool locksObtenidos1 = false;
                    while (locksObtenidos1 == false)
                    {
                        if (Monitor.TryEnter(RL_1))
                        {
                            try
                            {
                                if (Monitor.TryEnter(RL_3))
                                {
                                    try
                                    {
                                        RL_1 = -1;
                                        RL_3 = -1;
                                    }
                                    finally
                                    {
                                        Monitor.Exit(RL_3);
                                    }
                                }
                            }
                            finally
                            {
                                Monitor.Exit(RL_1);
                            }
                        }
                    }
                    break;
                case "Nucleo3":
                    //poner RL_2 y RL_1 en -1
                    bool locksObtenidos2 = false;
                    while (locksObtenidos2 == false)
                    {
                        if (Monitor.TryEnter(RL_1))
                        {
                            try
                            {
                                if (Monitor.TryEnter(RL_2))
                                {
                                    try
                                    {
                                        RL_1 = -1;
                                        RL_2 = -1;
                                    }
                                    finally
                                    {
                                        Monitor.Exit(RL_2);
                                    }
                                }
                            }
                            finally
                            {
                                Monitor.Exit(RL_1);
                            }
                        }
                    }
                    break;
            }

        }
        //COSAS POR HACER: corregir cuando hay más hilillos que Nucleos, cambiar vector de registros-sin RL


        /*--------------------------------------------------------------------*/
        private static void revisarSiCambioContexto()
        {
            if (quantum == quantum_total || mat_contextos[hilillo_actual - 1, 33] == 1)
            {
                string hiloActual = System.Threading.Thread.CurrentThread.Name;
                switch (hiloActual)
                {
                    case "Nucleo1":
                        int obtenido = 0;
                        while (obtenido == 0)
                        {
                            if (Monitor.TryEnter(RL_1))
                            {
                                try
                                {
                                    RL_1 = -1;
                                    obtenido = true;//COMENTARIO GRANDOTE ALGO MALO CON EL LOCK
                                    
                                }
                                finally
                                {
                                    Monitor.Exit(RL_1);
                                }
                            }
                            Interlocked.Add(ref obtenido, 1);
                        }
                        break;
                    case "Nucleo2":
                        int obtenido1 = 0;
                        while (obtenido1 == 0)
                        {
                            if (Monitor.TryEnter(RL_2))
                            {
                                try
                                {
                                    RL_2 = -1;
                                    Interlocked.Add(ref obtenido1, 1);
                                }
                                finally
                                {
                                    Monitor.Exit(RL_2);
                                }
                            }
                        }
                        break;
                    case "Nucleo3":
                        int obtenido2 = 0;
                        while (obtenido2 == 0)
                        {
                            if (Monitor.TryEnter(RL_3))
                            {
                                try
                                {
                                    RL_3 = -1;
                                    Interlocked.Add(ref obtenido2, 1);
                                }
                                finally
                                {
                                    Monitor.Exit(RL_3);
                                }
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
                    if (Monitor.TryEnter(mat_contextos))//PONER ADENTRO DE UN WHILE
                    {
                        try
                        {
                            hilillos_tomados.Remove(hilillo_actual);
                            hilillos_tomados.Add(indiceATomar + 1);  //poner numero de hilillo, correspondiente con el PC
                            mat_contextos[hilillo_actual - 1, 34] -= Int32.Parse(GetTimestamp(DateTime.Now));
                            hilillo_actual = indiceATomar + 1;
                            PC = (int)mat_contextos[indiceATomar, 32];
                            Console.WriteLine(System.Threading.Thread.CurrentThread.Name + " tomo el hilillo " + (indiceATomar + 1));
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