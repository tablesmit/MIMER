using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.IO;

using System.Linq;

using NUnit.Framework;
using MIMER;
using MIMER.RFC2045;
using NUnit.Framework.SyntaxHelpers;


namespace MIMERTests.RFC2045
{

    [TestFixture]
    public class MailReaderTests
    {
        private MailReader m_Reader;
        private System.IO.Stream m_Stream;
        private System.IO.Stream m_MultiMessageStream;
        private System.IO.Stream m_EmptyStream;
        private System.IO.Stream m_AttachmentStream;
        private System.IO.Stream m_EmbeddedMessagaesStream;
        private System.IO.Stream m_EmbeddedWithAttachmentsStream;
        private IEndCriteriaStrategy m_EndOfMessageStrategy;

        [SetUp]
        public void TestSetup()
        {
            m_EndOfMessageStrategy = new BasicEndOfMessageStrategy();
            m_Reader = new MailReader();
            char[] ar = MIMERTests.Strings.MailMessage1.ToCharArray();
            byte[] bar = Array.ConvertAll<char, byte>(ar, new Converter<char, byte>(
                delegate(char input) { return (byte)input; }));
            m_Stream = new MemoryStream(bar);

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < 100; i++)
            {
                builder.Append(MIMERTests.Strings.MailMessage1);
            }

            var multiMessageBytes = from char c in builder.ToString() select Convert.ToByte(c);
            m_MultiMessageStream = new MemoryStream(multiMessageBytes.ToArray());

            m_EmptyStream = new MemoryStream();

            IEnumerable<byte> mailMessageWithAttachmentBytes = from char c in Strings.MailMessageWithAttachment select Convert.ToByte(c);
            m_AttachmentStream = new MemoryStream(mailMessageWithAttachmentBytes.ToArray());

            m_EmbeddedMessagaesStream = 
                new MemoryStream(System.Text.Encoding.ASCII.GetBytes(
                MIMERTests.Strings.MailMessageWithRecursivlyEmbeddedMessage));

            m_EmbeddedWithAttachmentsStream = 
                new MemoryStream(System.Text.Encoding.ASCII.GetBytes(
                MIMERTests.Strings.MailMessageWithEmbeddedWithAttachments));

        }

        [Test]
        public void TestRead()
        {
            IMimeMailMessage message = m_Reader.ReadMimeMessage(ref m_Stream, m_EndOfMessageStrategy);
            Assert.AreEqual("nyhet@fl-net.se", message.From[0].Address);
            Assert.AreEqual("smithimage@home.se", message.To[0].Address);
            Assert.AreEqual(2, message.Body.Count);
            Assert.AreEqual("Mailbox.se - Gratis dataspel...", message.Subject);
            StringAssert.Contains("Syftet med detta kunskapsspel", message.TextMessage);
            Assert.IsFalse(message.IsNull());
        }

        [Test]
        public void TestReadAttachment()
        {
            IMimeMailMessage message = m_Reader.ReadMimeMessage(ref m_AttachmentStream, m_EndOfMessageStrategy);
            Assert.IsFalse(message.IsNull());
            Assert.AreEqual(3, message.Attachments.Count);
            Assert.AreEqual("mrx@unknown.com", message.From[0].Address);
            Assert.AreEqual("smithimage@home.se", message.To[0].Address);
            StringAssert.Contains("Software engineering", message.TextMessage);
            Assert.That(message.Attachments.Count(x=>x.Type != null && x.Type.Equals("application")), Iz.EqualTo(1));
            Assert.That(message.Attachments.Count(x => x.Name != null && x.Name.Equals("TDCOperatorGroup1.pdf")), Iz.EqualTo(1));
            Assert.That(message.Attachments.First(x => x.Name != null && x.Name.Equals("TDCOperatorGroup1.pdf")).Data.Length, Iz.GreaterThan(0));
        }

        [Test]
        public void TestSaveAttachment()
        {
            string directory = Environment.GetEnvironmentVariable("TEMP");
            m_AttachmentStream.Position = 0;
            IMimeMailMessage message = m_Reader.ReadMimeMessage(ref m_AttachmentStream, m_EndOfMessageStrategy);
            IAttachment attachment = message.Attachments.First(x => x.Name != null && x.Name.Equals("TDCOperatorGroup1.pdf"));
            File.WriteAllBytes(directory + attachment.Name, attachment.Data);
        }

        [Test]
        public void TestReadEmbeddedMessageAttachment()
        {
            m_EmbeddedMessagaesStream.Position = 0;
            IMimeMailMessage message = m_Reader.ReadMimeMessage(ref m_EmbeddedMessagaesStream, m_EndOfMessageStrategy);
            Assert.AreEqual(5, message.Attachments.Count);
            Assert.AreEqual(1, message.Messages.Count);
            Assert.AreEqual(1, message.Messages[0].Messages.Count);

            IMimeMailMessage firstEmbeddedMessage = message.Messages[0];            
            StringAssert.Contains("VB:", firstEmbeddedMessage.Subject);
            StringAssert.Contains("legtrace@smithimage.com", firstEmbeddedMessage.From[0].Address);
            StringAssert.Contains("Teatar med embedded message", firstEmbeddedMessage.Body["text/plain"]);
            Assert.AreEqual(2, firstEmbeddedMessage.Views.Count);

            IMimeMailMessage secondEmbeddedMessage = firstEmbeddedMessage.Messages[0];
            StringAssert.Contains("[CodeProject] Imbedded messages", secondEmbeddedMessage.Subject);
            StringAssert.Contains("noreply@mail.codeproject.com", secondEmbeddedMessage.From[0].Address);
            StringAssert.Contains("Do not hit 'reply' to this email:", secondEmbeddedMessage.Body["text/plain"]);
            Assert.AreEqual(2, secondEmbeddedMessage.Views.Count);
        }

        [Test]
        public void TestReadEmbeddedWithAttachments()
        {
            IMimeMailMessage message =
                m_Reader.ReadMimeMessage(ref m_EmbeddedWithAttachmentsStream, m_EndOfMessageStrategy);

            Assert.AreEqual(4, message.Attachments.Count);
            Assert.AreEqual(1, message.Messages.Count);

            Assert.That(message.Attachments.Count(x => x.Name != null && x.Name.Equals("hostels.txt")), Iz.EqualTo(1));
            Assert.That(message.Attachments.Count(x => x.Name != null && x.Name.Equals("o_aspNet_Page_LifeCycle.jpg")), Iz.EqualTo(1));

            IMimeMailMessage firstMessage = message.Messages[0];
            Assert.AreEqual(5, firstMessage.Attachments.Count);
            Assert.AreEqual(1, firstMessage.Messages.Count);

            Assert.That(firstMessage.Attachments.Count(x=>x.Name != null && x.Name.Equals("POP3ClientStates.vsd")), Iz.EqualTo(1));
            Assert.That(firstMessage.Attachments.Count(x => x.Name != null && x.Name.Equals("filmförslag.txt")), Iz.EqualTo(1));
            Assert.That(firstMessage.Attachments.Count(x => x.Name != null && x.Name.Equals("pleasewait.gif")), Iz.EqualTo(1));
            Assert.AreEqual("Test med flera olika", firstMessage.Subject);
            StringAssert.Contains("+46(0)123434534", firstMessage.TextMessage);

            IMimeMailMessage secondMessage = firstMessage.Messages[0];
            Assert.AreEqual(1, secondMessage.Messages.Count);

            Assert.AreEqual("VB: ", secondMessage.Subject);
            StringAssert.Contains("Teatar med embedded message", secondMessage.TextMessage);

            IMimeMailMessage thirdMessage = secondMessage.Messages[0];
            Assert.AreEqual("[CodeProject] Imbedded messages", thirdMessage.Subject);
            StringAssert.Contains("Great Article.", thirdMessage.TextMessage);            

        }

        [Test]
        public void TestMultiMessageRead()
        {
            for (int i = 0; i < 100; i++)
            {
                IMimeMailMessage message = m_Reader.ReadMimeMessage(ref m_MultiMessageStream, m_EndOfMessageStrategy);
                Assert.AreEqual("nyhet@fl-net.se", message.From[0].Address);
                Assert.AreEqual("smithimage@home.se", message.To[0].Address);
                Assert.AreEqual(2, message.Body.Count);
                Assert.AreEqual("Mailbox.se - Gratis dataspel...", message.Subject);
                StringAssert.Contains("Syftet med detta kunskapsspel", message.TextMessage);
            }
        }

        [Test]
        public void TestZeroLenghtStream()
        {
            IMimeMailMessage message = m_Reader.ReadMimeMessage(ref m_EmptyStream, m_EndOfMessageStrategy);
            Assert.IsTrue(message.IsNull());
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestNullArgument()
        {
            Stream stream = null;
            IMimeMailMessage message = m_Reader.ReadMimeMessage(ref stream, m_EndOfMessageStrategy);
            Assert.Fail("Should have caused ArgumentNullException");
        }

        [Test]
        public void TestReadChineseMail()
        {
            Stream big5Stream =
                new MemoryStream(System.Text.Encoding.ASCII.GetBytes(
                MIMERTests.Strings.MailMessage2));
            IMimeMailMessage message = m_Reader.ReadMimeMessage(ref big5Stream, m_EndOfMessageStrategy);

            Assert.IsNotNull(message);
            Assert.AreEqual("五 年 多", message.From[0].DisplayName);
            Assert.AreEqual("programmer@chilkatsoft.com", message.From[0].Address);
            Assert.AreEqual("五 年 多", message.To[0].DisplayName);
            Assert.AreEqual("support@chilkatsoft.com", message.To[0].Address);
            Assert.AreEqual("香 港 剛 結 束 長 ", message.Subject);
            StringAssert.Contains("香 港 剛 結 束 長 達 五 年 多", message.TextMessage);
            Assert.IsFalse(message.IsNull());

        }

        [Test]
        public void TestReadSMIMECertMessage()
        {
            Stream smimeMessagaeStream = new MemoryStream(Encoding.ASCII.GetBytes(Strings.SMIME_CERT));
            IMimeMailMessage message = m_Reader.ReadMimeMessage(ref smimeMessagaeStream, m_EndOfMessageStrategy);

            Assert.IsNotNull(message, "Message was null");
            Assert.AreEqual(3, message.Attachments.Count);
            Assert.That(message.Attachments.Count(x => x.Name != null && x.Name.Equals("smime.p7s")), Iz.EqualTo(1));
        }

        [Test]
        public void TestReadSMIMEMessage()
        {
            Stream smimeMessageStream = new MemoryStream(Encoding.ASCII.GetBytes(Strings.SMIME_MESSAGE));
            IMimeMailMessage message = m_Reader.ReadMimeMessage(ref smimeMessageStream, m_EndOfMessageStrategy);
            Assert.IsNotNull(message, "Message was null");
            Assert.AreEqual(1, message.Attachments.Count);
            Assert.AreEqual("smime.p7m", message.Attachments[0].Name);
        }

        [Test]
        public void TestReadNonStandardBoundary()
        {
            Stream boundaryStream = new MemoryStream(Encoding.ASCII.GetBytes(Strings.NonStandardBoundary));
            IMimeMailMessage message = m_Reader.ReadMimeMessage(ref boundaryStream, m_EndOfMessageStrategy);
            Assert.That(message, Iz.Not.Null);
            Assert.That(message.Body["text/plain"], Iz.Not.Null);
            Assert.That(message.Body["text/html"], Iz.Not.Null);
            Assert.That(message.Attachments.Count, Iz.GreaterThan(0));
        }

        [TearDown]
        public void TearDown()
        {
            m_Stream.Close();
            m_Stream.Dispose();

            m_MultiMessageStream.Close();
            m_MultiMessageStream.Dispose();

            m_EmptyStream.Close();
            m_EmptyStream.Dispose();
        }

        protected bool EndOfMessage(char[] data, int size)
        {
            if (size >= 4)
            {
                int fifth = data[size - 4];
                int fourth = data[size - 3];
                int third = data[size - 2];
                int second = data[size - 1];
                int first = data[size];

                //'CTRL.CTRL' indicates end of message
                if (fifth == 13 && fourth == 10 && third == 46 &&
                    second == 13 && first == 10)
                {
                    return true;
                }
            }

            if (size >= 2)
            {
                int third = data[0];
                int second = data[1];
                int first = data[2];

                //'.CTRL' indicates end of message
                if (third == 46 && second == 13 && first == 10)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
