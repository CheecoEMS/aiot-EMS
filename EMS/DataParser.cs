using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EMS
{
    /// <summary>
    /// 十六进制协议解析工具类（TryRead 模式）
    /// 不抛异常，失败返回 false
    /// </summary>
    public static class DataParser
    {
        #region ===== 基础 Hex 解析 =====

        /// <summary>
        /// 尝试解析 4 字符 Hex 为 UInt16
        /// </summary>
        public static bool TryHexToUInt16(string hex4, out ushort value)
        {
            value = default;

            if (!IsValidHex4(hex4))
                return false;

            try
            {
                value = Convert.ToUInt16(hex4, 16);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 尝试解析 4 字符 Hex 为 Int16（补码）
        /// </summary>
        public static bool TryHexToInt16(string hex4, out short value)
        {
            value = default;

            if (!IsValidHex4(hex4))
                return false;

            try
            {
                value = unchecked((short)Convert.ToUInt16(hex4, 16));
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region ===== 带比例因子 =====

        /// <summary>
        /// 尝试解析 4 字符 Hex 为 Int16 并乘以比例
        /// </summary>
        public static bool TryHexToInt16Scaled(string hex4, double scale, out double value)
        {
            value = default;

            if (!TryHexToInt16(hex4, out short raw))
                return false;

            value = raw * scale;
            return true;
        }

        /// <summary>
        /// 尝试解析 4 字符 Hex 为 UInt16 并乘以比例
        /// </summary>
        public static bool TryHexToUInt16Scaled(string hex4, double scale, out double value)
        {
            value = default;

            if (!TryHexToUInt16(hex4, out ushort raw))
                return false;

            value = raw * scale;
            return true;
        }

        #endregion

        #region ===== 从数据流中读取（推进指针） =====

        /// <summary>
        /// 从 data 中读取一个 Int16（推进 4 字符）
        /// </summary>
        public static bool TryReadInt16(ref string data, out short value)
        {
            value = default;

            if (!CanRead(data))
                return false;

            string hex = data.Substring(0, 4);
            data = data.Substring(4);

            return TryHexToInt16(hex, out value);
        }

        /// <summary>
        /// 从 data 中读取一个 UInt16（推进 4 字符）
        /// </summary>
        public static bool TryReadUInt16(ref string data, out ushort value)
        {
            value = default;

            if (!CanRead(data))
                return false;

            string hex = data.Substring(0, 4);
            data = data.Substring(4);

            return TryHexToUInt16(hex, out value);
        }

        /// <summary>
        /// 从 data 中读取一个 Int16（带比例）
        /// </summary>
        public static bool TryReadInt16Scaled(ref string data, double scale, out double value)
        {
            value = default;

            if (!TryReadInt16(ref data, out short raw))
                return false;

            value = raw * scale;
            return true;
        }

        /// <summary>
        /// 从 data 中读取一个 UInt16（带比例）
        /// </summary>
        public static bool TryReadUInt16Scaled(ref string data, double scale, out double value)
        {
            value = default;

            if (!TryReadUInt16(ref data, out ushort raw))
                return false;

            value = raw * scale;
            return true;
        }

        #endregion

        #region ===== 私有辅助方法 =====

        private static bool IsValidHex4(string hex4)
        {
            return !string.IsNullOrWhiteSpace(hex4) && hex4.Length == 4;
        }

        private static bool CanRead(string data)
        {
            return !string.IsNullOrEmpty(data) && data.Length >= 4;
        }

        #endregion
    }
}
