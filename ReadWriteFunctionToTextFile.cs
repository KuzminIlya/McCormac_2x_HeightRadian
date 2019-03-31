using System;
using System.IO;


// Класс, предназначеный для сохранения сетки в файл
public static class ReadWriteFunction
{

    // Сохранение в файл сетки вместе с начальными значеними и шагами
    public static void WriteGridInToFile(string path, double[,] RecArray, double h1, double h2, double StartPositionX, double StartPositionY)
    {
        int i, j;
        using (StreamWriter SW = File.CreateText(path))
        {
            SW.WriteLine(RecArray.GetLength(0));
            SW.WriteLine(RecArray.GetLength(1));
            SW.WriteLine(h1);
            SW.WriteLine(h2);
            SW.WriteLine(StartPositionX);
            SW.WriteLine(StartPositionY);
            for(i = 0; i < RecArray.GetLength(0); i++)
                for (j = 0; j < RecArray.GetLength(1); j++)
                {
                    SW.WriteLine(RecArray[i, j]);
                }
        }
    }

    // Сохранение сетки как табличной функции (учитывая начальные значения)
    public static void WriteFunctionInToFile(string path, double[,] RecordingArray, double h1, double h2, double StartPositionX, double StartPositionY)
    {
        int i, j;
        using (StreamWriter SW = File.CreateText(path))
        {
            SW.WriteLine(RecordingArray.Length);
            for (i = 0; i < RecordingArray.GetLength(0); i++)
                for (j = 0; j < RecordingArray.GetLength(1); j++)
                {
                    SW.WriteLine((i * h1 + StartPositionX).ToString() + " " + (j * h2 + StartPositionY).ToString() + " " + RecordingArray[i, j].ToString());
                }
        }
    }

    //сохранение сетки как табличной функции
    public static void WriteFunctionInToFile(string path, double[,] RecordingArray)
    {
        int i;
        using (StreamWriter SW = File.CreateText(path))
        {
            SW.WriteLine(RecordingArray.GetLength(1));
            for (i = 0; i < RecordingArray.GetLength(1); i++)
            {
                SW.WriteLine(RecordingArray[0, i].ToString() + " " + RecordingArray[1, i].ToString() + " " + RecordingArray[2, i].ToString());
            }
        }
    }


    // Процедуры чтения из файла
    public static void ReadFunctionFromFile(string path, ref double[,] Array)
    {
        int i;
        string temp;
        int space1, space2;
        int Len1, Len2;

        using (StreamReader SR = File.OpenText(path))
        {
            Array = new double[3, Convert.ToInt32(SR.ReadLine())];
            for (i = 0; i < Array.GetLength(1); i++)
            {
                temp = SR.ReadLine();
                space1 = temp.IndexOf(' ');
                space2 = temp.IndexOf(' ', space1 + 1);

                Len1 = space1;
                Len2 = space2 - space1 - 1;

                Array[0, i] = Convert.ToDouble(temp.Substring(0, Len1));
                Array[1, i] = Convert.ToDouble(temp.Substring(space1 + 1, Len2));
                Array[2, i] = Convert.ToDouble(temp.Substring(space2 + 1));
            }
        }
    }

    public static void ReadGridFromFile(string path, ref double[,] Arr, ref double h1, ref double h2, ref double StartX, ref double StartY)
    {
        int i, j, M, N;

        using (StreamReader SR = File.OpenText(path))
        {
            N = Convert.ToInt32(SR.ReadLine());
            M = Convert.ToInt32(SR.ReadLine());
            h1 = Convert.ToDouble(SR.ReadLine());
            h2 = Convert.ToDouble(SR.ReadLine());
            StartX = Convert.ToDouble(SR.ReadLine());
            StartY = Convert.ToDouble(SR.ReadLine());
            Arr = new double[N, M];
            for(i = 0; i < N; i++)
                for (j = 0; j < M; j++)
                {
                    Arr[i, j] = Convert.ToDouble(SR.ReadLine());
                }
        }
    }
}
