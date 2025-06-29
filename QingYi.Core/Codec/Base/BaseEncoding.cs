// Copyright (c) 2025 Tsing Yi Studio - Grey-Wind
using System;

namespace QingYi.Core.Codec.Base
{
    /// <summary>
    /// 字符串编码格式。<br />
    /// String encoding.
    /// </summary>
    [Flags]
    public enum StringEncoding
    {
        /// <summary>
        /// UTF-8
        /// </summary>
        UTF8,

        /// <summary>
        /// UTF-16 LE
        /// </summary>
        UTF16LE,

        /// <summary>
        /// UTF-16 BE
        /// </summary>
        UTF16BE,

        /// <summary>
        /// ASCII
        /// </summary>
        ASCII,

        /// <summary>
        /// UTF-32
        /// </summary>
        UTF32,

#if NET6_0_OR_GREATER
        /// <summary>
        /// Latin1
        /// </summary>
        Latin1,
#endif
        /// <summary>
        /// UTF-7
        /// </summary>
        [Obsolete(message: "UTF-7 has been deprecated because it is obsolete.")]
        UTF7,
    }
}
