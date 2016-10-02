using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Proyecto_Arqui
{
    class Program
    {
        //VARIABLES GLOBALES
        int[] mem_principal_datos; //array de la memoria principal de datos 
        int[] mem_principal_instruc; //array de la memoria principal de instrucciones
        int[][] mat_contextos; //matriz de contextos
        double ciclos_reloj; //lleva la cantidad de ciclos de reloj
        int cant_hilillos; //cantidad de hilillos definida por el usuario
        int quantum_total; //variable solo para almacenar el valor que el usuario da para el quantum.
        string file_path; //direccion del directorio donde estaran los hilillos

        //VARIABLES DE CADA NUCLEO (declarar en la creacion de cada hilo)
      /*int quantum;// lleva la cantidad de instrucciones que se ejecutan segun definio el usuario
        int[][] cache_datos; //matriz de cache de datos
        int[][] cache_instruc; //matriz de cache de instrucciones
        int[] registros;//registros propios del nucleo
        int PC;// se guarda la siguiente instruccion que se ejecutara
      */

        static void Main(string[] args)
        {
        }
    }
}
