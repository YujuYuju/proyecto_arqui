using System;
using System.Collections.Generic;
using System.Threading;

namespace Proyecto_Arqui
{
    class Program
    {
        //VARIABLES GLOBALES
        static int[] mem_principal_datos;       //memoria principal de datos 
        static int[] mem_principal_instruc;     //memoria principal de instrucciones
        static int[,] mat_contextos;            //matriz de contextos
        double ciclos_reloj;                    //cantidad de ciclos de reloj
        static int quantum_total;               //valor del usuario para quantum
        
        [ThreadStatic] static int quantum;          //cantidad de instrucciones ejecutadas
        [ThreadStatic] static int[,] cache_datos;   //matriz de cache de datos
        [ThreadStatic] static int[,] cache_instruc; //matriz de cache de instrucciones
        [ThreadStatic] static int[] registros;      //registros propios del nucleo
        [ThreadStatic] static int PC;               //la siguiente instruccion a ejecutar

        static List<int> hilillos_tomados;
        int ultimo_mem_inst;    //'puntero' a ultimo lleno en memoria de instrucciones
        static int cant_hilillos;      //cantidad de hilillos definida por el usuario
        string file_path;       //direccion del directorio donde estaran los hilillos

        /*Metodos direccionamiento de memoria--------------------------------*/
        private int dir_a_bloque(int direccion)
        {
            return direccion / 16;
        }
        private int dir_a_palabra(int direccion)
        {
            return (direccion % 16) / 4;
        }
        private int bloque_a_cache(int bloque)
        {
            return bloque % 4;
        }

        //Direccionamiento de Estructuras de Datos
        private int direccion_a_vectorDatos(int direccion)
        {
            return direccion / 4;
        }
        private int vectorDatos_a_direccion(int indiceVector)
        {
            return indiceVector * 4;
        }
        private int direccion_a_vectorInstrucciones(int direccion)
        {
            return direccion - 384;
        }
        private int vectorInstrucciones_a_direccion(int indiceVector)
        {
            return indiceVector + 384;
        }

        

        /*----------------------------------------------------------------------------------------------*/
        /*Inicio del programa-----------------------------------------------*/
        public Program() //constructor, se inicializan las variables
        {
            mem_principal_datos = new int[96];
            for (int i=0; i<=95; i++) {
                mem_principal_datos[i]= 1;
            }

            mem_principal_instruc = new int[640];
            for (int j = 0; j <= 639; j++)
            {
                mem_principal_instruc[j] = 1;
            }
			ultimo_mem_inst = 0;
            ciclos_reloj = 0;
            cant_hilillos = 0;
            quantum_total = 0;
            file_path = "../../../Hilillos/";
            hilillos_tomados = new List<int>();
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
        //Metodo auxiliar que sirve para leer un solo hilillo y meterlo en memoria
        private void leer_hilillo_txt(int i) 
        {
			char[] delimiterChars = { ' ', '\n'};
			string text = System.IO.File.ReadAllText(@file_path + i.ToString() + ".txt");
			string[] words = text.Split(delimiterChars);
			int ultimo_viejo = ultimo_mem_inst;

			foreach (string s in words)
			{
				mem_principal_instruc[ultimo_mem_inst++]= Int32.Parse(s);
			}
            mat_contextos[i-1, 32] = ultimo_viejo;   //el PC

        }
		public void leer_muchos_hilillos() { //permite cargar todos los hilillos desde el txt a memoria
            mat_contextos = new int[cant_hilillos, 34];
            for (int i = 1; i <= cant_hilillos; i++) {
				leer_hilillo_txt(i);
			}
            mem_principal_instruc[ultimo_mem_inst]=1;


            for (int i = 0; i < cant_hilillos; i++){
                for (int j = 0; j < 34; j++){
                    if (j != 32) {
                        mat_contextos[i, j] = 0;
                    }
                }
            }
        }

        
        /*Hilos----------------------------------------------------------*/
        static void escogerHilillo()
        {
            bool lockWasTaken = false;
            var temp = mem_principal_instruc;
            try{
                Monitor.Enter(temp, ref lockWasTaken);
                bool hilillo_escogido = false;
                for (int i=0; i< cant_hilillos && hilillo_escogido==false; i++) {
                    if (mat_contextos[i, 33] != 1) {
                        if (!hilillos_tomados.Contains(i + 1)) {
                            hilillos_tomados.Add(i + 1);  //poner numero de hilillo, correspondiente con el PC
                            PC = mat_contextos[i, 32];
                            hilillo_escogido = true;
                        }
                    }
                }
            }
            finally{
                if (lockWasTaken){
                    Monitor.Exit(temp);
                }
            }
        }
        private static void procesoDelNucelo()
        {
            //INICIALIZACION
            cache_datos = new int[6,4];
            for (int i = 0; i < 6; i++) {
                for (int j = 0; j < 4; j++) {
                    cache_datos[i, j] = 0;

                }
            }
            cache_instruc = new int[5, 16];
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    cache_instruc[i, j] = 0;

                }
            }
            registros = new int[34];
            quantum = 0;
            PC = 0;

            //PROCESO DEL NUCLEO
            escogerHilillo();
        }

        /*-------------------------------------------------------------------*/
        /*MAIN---------------------------------------------------------------*/
        static void Main(string[] args)
        {
            Program p = new Program();
            p.menu_usuario();
			p.leer_muchos_hilillos();
            /*//IMPRESION DE MEMORIA INSTRUCCIONES
            for (int i = 0; i < mem_principal_instruc.Length; i++) {
                if (mem_principal_instruc[i] != 1) {
                    Console.Write(mem_principal_instruc[i]+"  ");
                    if (i!=0 && (i+1)%4==0){
                        Console.WriteLine("\n");
                    }
                }
            }*/
            /*//IMPRESION DE MATRIZ DE CONTEXTOS
            for (int i = 0; i < cant_hilillos; i++)
            {
                Console.Write(mat_contextos[i, 32]+" ");
            }*/
            
            //crear nucleos
            Thread myThread;
            for (int i = 0; i < 3; i++)
            {
                myThread = new Thread(new ThreadStart(procesoDelNucelo));
                myThread.Name = String.Format("Hilo{0}", i + 1);
                myThread.Start();
            }
            Console.ReadKey();
        }
    }
}

/*links
 http://stackoverflow.com/questions/4190949/create-multiple-threads-and-wait-all-of-them-to-complete
 https://www.dotnetperls.com/interlocked
 https://msdn.microsoft.com/en-us/library/system.threading.interlocked.aspx
 http://stackoverflow.com/questions/6029804/how-does-lock-work-exactly
 http://dotnetpattern.com/threading-barrier
 http://www.codeproject.com/Articles/667298/Using-ThreadStaticAttribute
*/
