﻿
using System.Text.RegularExpressions;

namespace MIMER.RFC2047.Pattern
{
    public class EncodedWordPattern:IPattern
    {
        private readonly string m_TextPattern;
        private readonly Regex m_Regex;

        public EncodedWordPattern()
        {
            IPattern tokenPattern = PatternFactory.GetInstance().Get(typeof (RFC822.Pattern.TokenPattern));
            m_TextPattern = "=\x5C?" + tokenPattern.TextPattern + "\x5C?" + tokenPattern.TextPattern +
                "\x5C?[^\x3F\x20]+\x5C?=";
            m_Regex = new Regex(m_TextPattern, RegexOptions.Compiled);
        }

        public string TextPattern
        {
            get { return m_TextPattern; }
        }

        public Regex RegularExpression
        {
            get { return m_Regex; }
        }
    }
}
