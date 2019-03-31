using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.IO;

namespace McCormac_2x_HeightRadian
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        // Поля структуры: 
        // Val - значение параметра на временном слое, 
        // IntermVal - промежуточное значение для данного и следующего временных слоев
        public struct Value
        {
            public double Val, IntermVal;
        }

        public double h1, h2, tau,              // шаги по R, Z и времени
                      tk,                       // конечное время
                      alpha,                    // параметр сглаживания
                      sum_s = 0, sum_f = 0,     // переменные для расчетов массы газа в области
                      tau_save;                 // шаг сохранения по времени

        public double tau1, tau2, Smax;         // параметры модельного источника

        public int M, N,                        // Количество узлов сетки по R и Z
                   K,                           // Количество шагов по времени
                   P1, P2;                      // Для сохранения сетки в файл          

        public bool Heat;                       // Флаг показывающий, что расчеты ведутся с нагревом (true)
        double[,] A;                            // Массив для сохранения сетки
        public Value[,] n, u, v, T;             // Массивы расчитываемых параметров
        public string path;                     // Путь к каталогу сохранения

        delegate void SetTextCallback(Label txtBx, string text);    // Делегат для доступа к текстовому полю из потока с расчетами
        delegate void ProgBarInc(ProgressBar prB);                  // Делегат для доступа к полосе прогресса из потока с расчетами       


        // Запись сетки в массив для записи в файл
        public void AMass(Value[,] Arr, ref double[,] A)
        {
            for (int i = 0; i < M / P1; i++)
                for (int j = 0; j < N / P2; j++)
                {
                    A[i, j] = Arr[P1 * i, P2 * j].Val;
                }
        }

        // Модельный источник
        public double S(double r, double z, double t, double tau1, double tau2, double Smax, bool Heat)
        {
            if (Heat)
            {
                if (r > 2)
                    return 0;
                else
                    if (t <= tau2)
                    {
                        return (((Smax / tau1) * t) * Math.Exp(-Math.Pow(2 * z - 20, 2) - Math.Pow(r, 2)));
                    }
                    else
                    {
                        return 0;
                    }
            }
            else
            {
                return 0;
            }
        }


        // Начальное температурное распределение
        public double T_gaus(double r, double z)
        {
            if (r > 2)
                return 1;
            else
                return (33*Math.Exp(-Math.Pow(2 * z - 20, 2) - Math.Pow(r, 2))) + 1;

        }


        // Процедура сглаживания
        public void smoothing(ref Value[,] Arr, int A, int B)
        {
            double[,] Mass = new double[A, B];

            for (int i = 1; i < A - 1; i++)
                for (int j = 1; j < B - 1; j++)
                {
                    Mass[i, j] = (1 - 4 * alpha) * Arr[i, j].Val + alpha * Arr[i - 1, j].Val + alpha * Arr[i, j - 1].Val +
                        alpha * Arr[i + 1, j].Val + alpha * Arr[i, j + 1].Val;
                }

            for (int i = 1; i < A - 1; i++)
                for (int j = 1; j < B - 1; j++)
                {
                    Arr[i, j].Val = Mass[i, j];
                }
        }


        // Процедура вывода в текстовое поле из потока с расчетами
        public void SetText(Label txtBx, string text)
        {
            if (this.textBox1.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText);
                this.Invoke(d, new object[] { txtBx, text });
            }
            else
            {
                txtBx.Text = text;
            }
        }

        // Процедура обращения к полосе загрузки из потока с расчетами
        public void ProgressBarInc(ProgressBar prB)
        {
            if (this.progressBar1.InvokeRequired)
            {
                ProgBarInc d = new ProgBarInc(ProgressBarInc);
                this.Invoke(d, new object[] { prB });
            }
            else
            {
                prB.PerformStep();
            }
        }

        // Процедура запускаемая во втором потоке. Содержит основные расчеты.
        public void Calculate()
        {
            DateTime Time = new DateTime(); // Для оценки оставшегося времени
            int TimeS_m, TimeS_s, TimeF_m, TimeF_s; //Стартовое и Конечное время для одной итерации цикла с расчетами
            int DeltTime, TimeOstM, TimeOstS; // Для расчетов интервала времени на одну итерацию

            //Начальные условия
            sum_s = 0;
            for (int i = 0; i < M; i++)
                for (int j = 0; j < N; j++)
                {
                    if (Heat)
                    {
                        T[i, j].Val = 1;
                    }
                    else
                    {
                        T[i, j].Val = T_gaus(i * h1, (j + 1) * h2 + 4);
                    }
                    T[i, j].IntermVal = T[i, j].Val;
                    n[i, j].Val = 1;
                    n[i, j].IntermVal = 1;
                    u[i, j].Val = 0;
                    u[i, j].IntermVal = 0;
                    v[i, j].Val = 0;
                    v[i, j].IntermVal = 0;
                    sum_s += n[i, j].Val;
                }
            // ВЫчисление начальной массы газа в области
            sum_s *= h1 * h2;
            SetText(label6, "M (t = 0) = " + sum_s.ToString("000.0000"));

            // Интервалы сохранения
            int Step_Save = Convert.ToInt32(tau_save / tau);
            int k_save = Step_Save;

            // Основной цикл
            for (int k = 1; k <= K; k++)
            {
                Time = DateTime.Now;
                TimeS_m = Time.Minute;
                TimeS_s = Time.Second;

                // ПРЕДИКТОР
                for (int i = 1; i < M - 1; i++)
                    for (int j = 1; j < N - 1; j++)
                    {
                        n[i, j].IntermVal = n[i, j].Val -
                        (tau / (i * h1)) * ((i + 1) * n[i + 1, j].Val * v[i + 1, j].Val - i * n[i, j].Val * v[i, j].Val) -
                        (tau / h2) * (n[i, j + 1].Val * u[i, j + 1].Val - n[i, j].Val * u[i, j].Val) + tau * n[i, j].Val * u[i, j].Val;

                        v[i, j].IntermVal = v[i, j].Val -
                        (tau / h1) * ((1 / n[i, j].Val) * (n[i + 1, j].Val * T[i + 1, j].Val - n[i, j].Val * T[i, j].Val) +
                        v[i, j].Val * (v[i + 1, j].Val - v[i, j].Val)) - (tau / h2) * u[i, j].Val * (v[i, j + 1].Val - v[i, j].Val);

                        u[i, j].IntermVal = u[i, j].Val -
                        (tau / h1) * v[i, j].Val * (u[i + 1, j].Val - u[i, j].Val) - (tau / h2) * ((1 / n[i, j].Val) *
                        (n[i, j + 1].Val * T[i, j + 1].Val - n[i, j].Val * T[i, j].Val) +
                        u[i, j].Val * (u[i, j + 1].Val - u[i, j].Val)) + tau * (T[i, j].Val - 1);

                        T[i, j].IntermVal = T[i, j].Val -
                        (tau / h1) * (v[i, j].Val * (T[i + 1, j].Val - T[i, j].Val) + (2.0 / (5 * i)) *
                        ((i + 1) * v[i + 1, j].Val - i * v[i, j].Val)) -
                        (tau / h2) * (u[i, j].Val * (T[i, j + 1].Val - T[i, j].Val) + (2.0 / 5.0) * T[i, j].Val *
                        (u[i, j + 1].Val - u[i, j].Val)) +
                        tau * S(i * h1, (j + 1) * h2 + 4, k * tau, tau1, tau2, Smax, Heat);
                    }



                // Граничные условия предиктора для температуры и плотности на оси
                for (int j = 1; j < N - 1; j++)
                {
                    n[0, j].IntermVal = n[1, j].IntermVal;
                    T[0, j].IntermVal = T[1, j].IntermVal;
                }

                // И для вертикальной скорости на оси
                for (int j = 1; j < N - 1; j++)
                {
                    u[0, j].IntermVal = u[0, j].Val - (tau / h2) * ((1 / n[0, j].Val) *
                        (n[0, j + 1].Val * T[0, j + 1].Val - n[0, j].Val * T[0, j].Val) +
                        u[0, j].Val * (u[0, j + 1].Val - u[0, j].Val)) + tau * (T[0, j].Val - 1);
                }

                // КОРРЕКТОР
                for (int i = 1; i < M - 1; i++)
                    for (int j = 1; j < N - 1; j++)
                    {
                        n[i, j].Val = 0.5 * (n[i, j].IntermVal + n[i, j].Val) -
                            (tau / (2 * i * h1)) * (i * n[i, j].IntermVal * v[i, j].IntermVal -
                            (i - 1) * n[i - 1, j].IntermVal * v[i - 1, j].IntermVal) -
                            (tau / (2 * h2)) * (n[i, j].IntermVal * u[i, j].IntermVal - n[i, j - 1].IntermVal *
                            u[i, j - 1].IntermVal) + (tau / 2) * n[i, j].IntermVal * u[i, j].IntermVal;

                        v[i, j].Val = 0.5 * (v[i, j].IntermVal + v[i, j].Val) -
                            (tau / (2 * h1)) * ((1 / n[i, j].IntermVal) * (n[i, j].IntermVal * T[i, j].IntermVal -
                            n[i - 1, j].IntermVal * T[i, j].IntermVal) +
                            v[i, j].IntermVal * (v[i, j].IntermVal - v[i - 1, j].IntermVal)) -
                            (tau / (2 * h2)) * (u[i, j].IntermVal * (v[i, j].IntermVal - v[i, j - 1].IntermVal));

                        u[i, j].Val = 0.5 * (u[i, j].IntermVal + u[i, j].Val) - (tau / (2 * h1)) * (v[i, j].IntermVal *
                            (u[i, j].IntermVal - u[i - 1, j].IntermVal)) - (tau / (2 * h2)) * ((1 / n[i, j].IntermVal) *
                            (n[i, j].IntermVal * T[i, j].IntermVal - n[i, j - 1].IntermVal * T[i, j].IntermVal) +
                            u[i, j].IntermVal * (u[i, j].IntermVal - u[i, j - 1].IntermVal)) + (tau / 2) * (T[i, j].IntermVal - 1);

                        T[i, j].Val = 0.5 * (T[i, j].IntermVal + T[i, j].Val) -
                            (tau / (2 * h1)) * (v[i, j].IntermVal * (T[i, j].IntermVal - T[i - 1, j].IntermVal) +
                            (2.0 / (5 * i)) * (T[i, j].IntermVal * (i * v[i, j].IntermVal - (i - 1) * v[i - 1, j].IntermVal))) -
                            (tau / (2 * h2)) * (u[i, j].IntermVal * (T[i, j].IntermVal - T[i, j - 1].IntermVal) +
                            (2.0 / 5.0) * T[i, j].IntermVal * (u[i, j].IntermVal - u[i, j - 1].IntermVal)) + (tau / 2) *
                            S(i * h1, (j + 1) * h2 + 4, k * tau, tau1, tau2, Smax, Heat);

                    }

                // Граничные условия корректора для температуры и плотности на оси
                for (int j = 1; j < N - 1; j++)
                {
                    n[0, j].Val = n[1, j].Val;
                    T[0, j].Val = T[1, j].Val;
                }

                // И для вертикальной скорости на оси
                for (int j = 1; j < N - 1; j++)
                {
                    u[0, j].Val = 0.5 * (u[0, j].IntermVal + u[0, j].Val) -
                            (tau / (2 * h2)) * ((1 / n[0, j].IntermVal) * (n[0, j].IntermVal *
                            T[0, j].Val - n[0, j - 1].IntermVal * T[0, j - 1].Val) +
                            u[0, j].IntermVal * (u[0, j].IntermVal - u[0, j - 1].IntermVal)) +
                            (tau / 2) * (T[0, j].IntermVal - 1);
                }

                //сглаживание
                smoothing(ref n, M, N);
                smoothing(ref u, M, N);
                smoothing(ref v, M, N);
                smoothing(ref T, M, N);

                // Сохранение в файл
                if (k_save == k)
                {
                    A = new double[M / P1, N / P2];
                    AMass(T, ref A);
                    ReadWriteFunction.WriteGridInToFile(path + "\\T\\T_t=" + (k * tau).ToString("00.000") + ".txt", A, P1 * h1, P2 * h2, 0, 4);
                    AMass(n, ref A);
                    ReadWriteFunction.WriteGridInToFile(path + "\\Ro\\n_t=" + (k * tau).ToString("00.000") + ".txt", A, P1 * h1, P2 * h2, 0, 4);
                    AMass(u, ref A);
                    ReadWriteFunction.WriteGridInToFile(path + "\\U\\U_t=" + (k * tau).ToString("00.000") + ".txt", A, P2 * h1, P2 * h2, 0, 4);
                    AMass(v, ref A);
                    ReadWriteFunction.WriteGridInToFile(path + "\\V\\V_t=" + (k * tau).ToString("00.000") + ".txt", A, P1 * h1, P2 * h2, 0, 4);
                    k_save += Step_Save;
                }

                // Оценка оставшегося времени
                Time = DateTime.Now;
                TimeF_m = Time.Minute;
                TimeF_s = Time.Second;
                DeltTime = (TimeF_m * 60 + TimeF_s) - (TimeS_m * 60 + TimeS_s);
                TimeOstS = DeltTime * (K - k);
                TimeOstM = Math.DivRem(TimeOstS, 60, out TimeOstS); 
                SetText(label11, "Осталось: " + TimeOstM.ToString() + " min " + TimeOstS.ToString() +" sec");
                ProgressBarInc(progressBar1);
            }

            // Расчет массы газа после проведения вычислений
            sum_f = 0;
            for (int i = 0; i < M; i++)
                for (int j = 0; j < N; j++)
                {
                    sum_f += n[i, j].Val;
                }
            sum_f *= h1 * h2;

            // Вывод массы и оценка отклонения от первоначального значения
            SetText(label7, "M (t = "+tk.ToString("00.000")+") = " + sum_f.ToString("000.0000") + "; Delta = " + ((Math.Abs(sum_f - sum_s) / Math.Max(sum_f, sum_s)) * 100).ToString("000.00000") + " %;");

            // Сохранение значений в конечный момент времени
            A = new double[M / P1, N / P2];
            AMass(T, ref A);
            ReadWriteFunction.WriteGridInToFile(path + "\\T\\T_t=" + tk.ToString("00.000") + ".txt", A, P1 * h1, P2 * h2, 0, 4);
            AMass(n, ref A);
            ReadWriteFunction.WriteGridInToFile(path + "\\Ro\\n_t=" + tk.ToString("00.000") + ".txt", A, P1 * h1, P2 * h2, 0, 4);
            AMass(u, ref A);
            ReadWriteFunction.WriteGridInToFile(path + "\\U\\U_t=" + tk.ToString("00.000") + ".txt", A, P1 * h1, P2 * h2, 0, 4);
            AMass(v, ref A);
            ReadWriteFunction.WriteGridInToFile(path + "\\V\\V_t=" + tk.ToString("00.000") + ".txt", A, P1 * h1, P2 * h2, 0, 4);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            
            //Ввод данных
            h1 = Convert.ToDouble(textBox1.Text);
            h2 = Convert.ToDouble(textBox2.Text);
            tau = Convert.ToDouble(textBox3.Text);
            tau_save = Convert.ToDouble(textBox9.Text);
            alpha = Convert.ToDouble(textBox4.Text);
            tk = Convert.ToDouble(textBox5.Text);

            tau1 = Convert.ToDouble(textBox8.Text);
            tau2 = Convert.ToDouble(textBox7.Text);
            Smax = Convert.ToDouble(textBox6.Text);

            M = Convert.ToInt32(12 / h1);
            N = Convert.ToInt32(16 / h2);
            K = Convert.ToInt32(tk / tau);

            n = new Value[M, N];
            u = new Value[M, N];
            v = new Value[M, N];
            T = new Value[M, N];

            Heat = radioButton1.Checked;

            P1 = Convert.ToInt32(Convert.ToDouble(textBox10.Text) / h1);
            P2 = Convert.ToInt32(Convert.ToDouble(textBox11.Text) / h2);

            progressBar1.Value = 0;
            progressBar1.Maximum = K;
            progressBar1.Step = 1;

            // Создание и запуск дополнительного потока, содержащего основные расчеты
            Thread Calc = new Thread(Calculate);
            Calc.Start();
            
        }

        // Выбор каталога сохранения (обязательно)
        private void button2_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                path = folderBrowserDialog1.SelectedPath;
                textBox12.Text = path;
                Directory.CreateDirectory(path + "\\Ro");
                Directory.CreateDirectory(path + "\\T");
                Directory.CreateDirectory(path + "\\U");
                Directory.CreateDirectory(path + "\\V");
            }


        }

    }
}