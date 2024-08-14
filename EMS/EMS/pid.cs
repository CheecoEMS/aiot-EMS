/** *****************************************************************************************
  * @file    pid.c                                                                          *
  * @author  swp                                                                            *
  * @version V1.0.0                                                                         *
  * @date    27-August-2013                                                                 *
  * @brief   pid的具体实现。实现过程完全与硬件无关                                               *
  *   可直接调用，任意移植                                                                     *
  *   使用时先定一个pid对象，然后调用PID_ini()初始化                                             *
  *   每次读取到传感器数据后即可调用PID_calc()来对数据进行处理                                     *
  *****************************************************************************************
  *                          使用示例                                                      *
  *                                                                                       *
  *          float SersorData;                                                            *
  *          float SetData = 10；                                                         *
  *          //pid模式,pid三参数,pid最大输出, pid最大积分输出                                 *
  *          PID_ini( _mode,  PID,  _max_out,  _max_iout) ;                               *
  *          while(1)                                                                     *
  *          {                                                                            *
  *             SersorData = sersor();                                                    *
  *             SersorData = PID_calc(SersorData, SetData );    //测量值,目标值             *
  *             printf("%2.2f",SersorData);                                               *
  *          }                                                                            *
  *****************************************************************************************
  *          MPU6050的参考参数 P：10 I：40 D:0                                             *
  *****************************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EMS
{
    public class PID
    {
    
       public enum PID_MODE
        {
            PID_POSITION = 0,
            PID_DELTA
        };

        private int mode;
        //PID 三参数
        private double Kp;
        private double Ki;
        private double Kd;

        private double max_out;  //最大输出
        private double max_iout; //最大积分输出

        private double set;
        private double fdb;

        private double allout;
        private double Pout;
        private double Iout;
        private double Dout;
        private double[] Dbuf = new double[3];  //微分项 0最新 1上一次 2上上次
        private double[] error = new double[3]; //误差项 0最新 1上一次 2上上次



        private void LimitMax(ref double input, double max)
        {
            if (input > max)
            {
                input = max;
            }
            else if (input < -max)
            {
                input = -max;
            }
        }


        /**
          * @brief          pid struct data init
          * @param[out]     pid: PID结构数据指针
          * @param[in]      mode: PID_POSITION:普通PID
          *                 PID_DELTA: 差分PID
          * @param[in]      PID: 0: kp, 1: ki, 2:kd
          * @param[in]      max_out: pid最大输出
          * @param[in]      max_iout: pid最大积分输出
          * @retval         none
          */
        public void PID_init(int _mode, double[] PID, double _max_out, double _max_iout)
        {

            mode = _mode;
            Kp = PID[0];
            Ki = PID[1];
            Kd = PID[2];
            max_out = _max_out;
            max_iout = _max_iout;
            Dbuf[0] = Dbuf[1] = Dbuf[2] = 0.0f;
            error[0] = error[1] = error[2] = Pout = Iout = Dout = allout = 0.0f;
        }


        /**
          * @brief          pid计算
          * @param[allout]     pid: PID结构数据指针
          * @param[in]      ref: 反馈数据
          * @param[in]      set: 设定值
          * @retval         pid输出
          */
        public double PID_calc( double _ref, double _set)
        {

            error[2] = error[1];
            error[1] = error[0];
            set = _set;
            fdb = _ref;
            error[0] = _set - _ref;
            if (mode == (int)PID_MODE.PID_POSITION)
            {
                Pout = Kp * error[0];
                Iout += Ki * error[0];
                Dbuf[2] = Dbuf[1];
                Dbuf[1] = Dbuf[0];
                Dbuf[0] = (error[0] - error[1]);
                Dout = Kd * Dbuf[0];
                //积分限幅
                LimitMax(ref Iout, max_iout);
                //积分分离
                


                allout = Pout + Iout + Dout;
                LimitMax(ref allout, max_out);
            }
            else if (mode == (int)PID_MODE.PID_DELTA)
            {
                Pout = Kp * (error[0] - error[1]);
                Iout = Ki * error[0];
                Dbuf[2] = Dbuf[1];
                Dbuf[1] = Dbuf[0];
                Dbuf[0] = (error[0] - 2.0f * error[1] + error[2]);
                Dout = Kd * Dbuf[0];
                allout += Pout + Iout + Dout;
                LimitMax(ref allout,  max_out);
            }
            return allout;
        }


        /**
          * @brief          pid 输出清除
          * @param[out]     pid: PID结构数据指针
          * @retval         none
          */
        void PID_clear()
        {
            error[0] = error[1] = error[2] = 0.0f;
            Dbuf[0] = Dbuf[1] = Dbuf[2] = 0.0f;
            allout = Pout = Iout = Dout = 0.0f;
            fdb = set = 0.0f;
        }






    }
}
