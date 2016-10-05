﻿using System;
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
		int ultimo_mem_inst; //'puntero' al ultimo lleno en la memoria de instrucciones
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

        }

        public void menu_usuario() //se manejan las preguntas iniciales al usuario
        {
            Console.WriteLine("\nCuantos hilillos?");
            cant_hilillos = Int32.Parse(Console.ReadLine());
            Console.WriteLine("\nRecuerde que el .txt de cada hilillo debe estar en la carpeta 'Hilillos'");
            Console.WriteLine("\nDe cuanto será el quantum?");
            quantum_total = Int32.Parse(Console.ReadLine());

			int size = cant_hilillos * 34;
			mat_contextos = new int[size, size];
			for (int i = 0; i <= size - 1; i++)
			{
				for (int j = 0; j <= size - 1; j++)
				{
					mat_contextos[i, j] = 0;
				}
			}
        }

		private void leer_hilillo_txt(int i) //metodo auxiliar que sirve para leer un solo hilillo y meterlo en memoria
        {
			char[] delimiterChars = { ' ', '\n'};
			string text = System.IO.File.ReadAllText(@file_path + i.ToString() + ".txt");
			string[] words = text.Split(delimiterChars);
			int ultimo_viejo = ultimo_mem_inst;

			foreach (string s in words)
			{
				mem_principal_instruc[ultimo_mem_inst++]= Int32.Parse(s);
			}
			mem_principal_instruc[ultimo_mem_inst++] = ultimo_viejo;

		}

		public void leer_muchos_hilillos() { //permite cargar todos los hilillos desde el txt a memoria
			for (int i = 1; i <= cant_hilillos; i++) {
				leer_hilillo_txt(i);
			}	
		}

        static void Main(string[] args)
        {
            Program p = new Program();
            p.menu_usuario();
			p.leer_muchos_hilillos();
        }
    }
}
