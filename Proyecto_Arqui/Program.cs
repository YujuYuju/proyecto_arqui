using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Proyecto_Arqui
{
    class Program
    {
        //VARIABLES GLOBALES
        int[] mem_principal_datos; //array de la memoria principal de datos 
        int[] mem_principal_instruc; //array de la memoria principal de instrucciones
        int[,] mat_contextos; //matriz de contextos
        double ciclos_reloj; //lleva la cantidad de ciclos de reloj
        int cant_hilillos; //cantidad de hilillos definida por el usuario
        int quantum_total; //variable solo para almacenar el valor que el usuario da para el quantum.
        string file_path; //direccion del directorio donde estaran los hilillos

        //VARIABLES DE CADA NUCLEO (declarar en la creacion de cada hilo)
        /*int quantum;// lleva la cantidad de instrucciones que se ejecutan segun definio el usuario
          int[,] cache_datos; //matriz de cache de datos, inicializar con ceros y con todo invalido
          int[,] cache_instruc; //matriz de cache de instrucciones, inicializar con ceros y con todo invalido
          int[] registros;//registros propios del nucleo
          int PC;// se guarda la siguiente instruccion que se ejecutara

        */

        public Program()
        {
            mem_principal_datos = new int[96];
            for (int i=0; i<=95; i++) {
                mem_principal_datos[i]= 1;
            }

            mem_principal_instruc = new int[640];
            for (int j = 0; j <= 640; j++)
            {
                mem_principal_instruc[j] = 1;
            }
            ciclos_reloj = 0;
            cant_hilillos = 0;
            quantum_total = 0;
            file_path = "..\\proyecto_arqui\\Hilillos\\";

        }

        public void menu_usuario()
        {
            Console.WriteLine("\nCuantos hilillos?");
            cant_hilillos = Int32.Parse(Console.ReadLine());
            mat_contextos = new int[cant_hilillos * 34, cant_hilillos * 34];

            Console.WriteLine("\nRecuerde que el .txt de cada hilillo debe estar en la carpeta 'Hilillos'");
            Console.WriteLine("\nDe cuanto será el quantum?");
            quantum_total = Int32.Parse(Console.ReadLine());
        }
        public void leer_hilillo_txt(int i)
        {
            foreach (string line in File.ReadLines(@file_path+i.ToString()+".txt", Encoding.UTF8))
            {
                // process the line
            }
        }

        static void Main(string[] args)
        {
            Program p = new Program();
            p.menu_usuario();
        }
    }
}
