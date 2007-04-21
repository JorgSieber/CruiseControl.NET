using System;
using System.Collections;
using System.Net.Mail;
using Exortech.NetReflector;
using NMock;
using NMock.Constraints;
using NUnit.Framework;
using ThoughtWorks.CruiseControl.Core;
using ThoughtWorks.CruiseControl.Core.Publishers;
using ThoughtWorks.CruiseControl.Core.Util;
using ThoughtWorks.CruiseControl.Remote;

namespace ThoughtWorks.CruiseControl.UnitTests.Core.Publishers
{
	[TestFixture]
	public class EmailPublisherTest : CustomAssertion
	{
		private EmailPublisher publisher;
		private IMock mockGateway;

		[SetUp]
		public void SetUp()
		{
			publisher = EmailPublisherMother.Create();
			mockGateway = new DynamicMock(typeof(EmailGateway));
			publisher.EmailGateway = (EmailGateway) mockGateway.MockInstance;
		}

		[Test]
		public void SendMessage()
		{
            mockGateway.Expect("Send", new MailMessageValidator());

			publisher.SendMessage("from@foo.com", "to@bar.com", "replyto@bar.com", "test subject", "test message");
            mockGateway.Verify();
		}

	    [Test]
		public void ShouldNotSendMessageIfRecipientIsNotSpecifiedAndBuildIsSuccessful()
		{
            mockGateway.ExpectNoCall("Send", typeof(MailMessage));
			publisher = new EmailPublisher();
			publisher.EmailGateway = (EmailGateway) mockGateway.MockInstance;
			publisher.EmailUsers.Add("bar", new EmailUser("bar", "foo", "bar@foo.com"));
			publisher.EmailGroups.Add("foo", new EmailGroup("foo", EmailGroup.NotificationType.Change));
			publisher.Run(IntegrationResultMother.CreateStillSuccessful());
            mockGateway.Verify();
		}

		[Test]
		public void ShouldSendMessageIfRecipientIsNotSpecifiedAndBuildFailed()
		{
            mockGateway.Expect("Send", new MailMessageRecipientValidator(1));

			publisher = new EmailPublisher();
		    publisher.FromAddress = "from@foo.com";
			publisher.EmailGateway = (EmailGateway) mockGateway.MockInstance;
			publisher.EmailUsers.Add("bar", new EmailUser("bar", "foo", "bar@foo.com"));
			publisher.EmailGroups.Add("foo", new EmailGroup("foo", EmailGroup.NotificationType.Change));
			publisher.Run(IntegrationResultMother.CreateFailed());
            mockGateway.Verify();
		}

	    [Test]
		public void ShouldSendMessageIfBuildFailed()
		{
            mockGateway.Expect("Send", new MailMessageRecipientValidator(1));

            publisher = new EmailPublisher();
            publisher.FromAddress = "from@foo.com";
			publisher.EmailGateway = (EmailGateway) mockGateway.MockInstance;
			publisher.EmailUsers.Add("bar", new EmailUser("bar", "foo", "bar@foo.com"));
			publisher.EmailGroups.Add("foo", new EmailGroup("foo", EmailGroup.NotificationType.Failed));
			publisher.Run(IntegrationResultMother.CreateFailed() );
            mockGateway.Verify();
        }

		[Test]
		public void ShouldSendMessageIfBuildFailedAndPreviousFailed()
		{
            mockGateway.Expect("Send", new MailMessageRecipientValidator(1));
            
            publisher = new EmailPublisher();
            publisher.FromAddress = "from@foo.com";
            publisher.EmailGateway = (EmailGateway)mockGateway.MockInstance;
			
			publisher.EmailUsers.Add("dev", new EmailUser("dev", "changing", "dev@foo.com"));
			publisher.EmailUsers.Add("admin", new EmailUser("admin", "failing", "bar@foo.com"));

			publisher.EmailGroups.Add("changing", new EmailGroup("changing", EmailGroup.NotificationType.Change));
			publisher.EmailGroups.Add("failing", new EmailGroup("failing", EmailGroup.NotificationType.Failed));

			publisher.Run(IntegrationResultMother.CreateFailed(IntegrationStatus.Failure) );
            mockGateway.Verify();
		}

		[Test]
		public void ShouldSendMessageIfBuildFailedAndPreviousOK()
		{
            mockGateway.Expect("Send", new MailMessageRecipientValidator(2));
            
            publisher = new EmailPublisher();
            publisher.FromAddress = "from@foo.com";
            publisher.EmailGateway = (EmailGateway)mockGateway.MockInstance;
			
			publisher.EmailUsers.Add("dev", new EmailUser("dev", "changing", "dev@foo.com"));
			publisher.EmailUsers.Add("admin", new EmailUser("admin", "failing", "bar@foo.com"));

			publisher.EmailGroups.Add("changing", new EmailGroup("changing", EmailGroup.NotificationType.Change));
			publisher.EmailGroups.Add("failing", new EmailGroup("failing", EmailGroup.NotificationType.Failed));

			publisher.Run(IntegrationResultMother.CreateFailed(IntegrationStatus.Success) );
            mockGateway.Verify();
        }

		private static IntegrationResult CreateIntegrationResult(IntegrationStatus current, IntegrationStatus last)
		{
			IntegrationResult result = IntegrationResultMother.Create(current, last, new DateTime(1980, 1, 1));
			result.ProjectName = "Project#9";
			result.Label = "0";
			return result;
		}

		[Test]
		public void EmailMessageWithDetails()
		{
			publisher.IncludeDetails = true;
			string message = publisher.CreateMessage(CreateIntegrationResult(IntegrationStatus.Success, IntegrationStatus.Success));
			Assert.IsTrue(message.StartsWith("<html>"));
			Assert.IsTrue(message.IndexOf("CruiseControl.NET Build Results for project Project#9") > 0);
			Assert.IsTrue(message.IndexOf("Modifications since last build") > 0);
			Assert.IsTrue(message.EndsWith("</html>"));
		}

		[Test]
		public void IfThereIsAnExceptionBuildMessageShouldPublishExceptionMessage()
		{
			DynamicMock mock = new DynamicMock(typeof(IMessageBuilder));
			mock.ExpectAndThrow("BuildMessage", new Exception("oops"), new IsAnything());
			publisher = new EmailPublisher((IMessageBuilder) mock.MockInstance);
			string message = publisher.CreateMessage(new IntegrationResult());
			AssertContains("oops", message);
		}

		[Test]
		public void Publish()
		{
//            mockGateway.Expect("MailHost", "mock.gateway.org");
            mockGateway.Expect("Send", new IsAnything());
			IntegrationResult result = IntegrationResultMother.CreateStillSuccessful();
			publisher.Run(result);
            mockGateway.Verify();
		}

		[Test]
		public void UnitTestResultsShouldBeIncludedInEmailMessageWhenIncludesDetailsIsTrue()
		{
			IntegrationResult result = IntegrationResultMother.CreateStillSuccessful();
			string results = "<test-results name=\"foo\" total=\"10\" failures=\"0\" not-run=\"0\"><test-suite></test-suite></test-results>";
			result.AddTaskResult(results);
			publisher.IncludeDetails = true;
			string message = publisher.CreateMessage(result);
			Assert.IsTrue(message.IndexOf("Tests run") >= 0);
		}

		[Test]
		public void Publish_UnknownIntegrationStatus()
		{
            mockGateway.ExpectNoCall("Send", typeof(MailMessage));
			publisher.Run(new IntegrationResult());
			// verify that no messages are sent if there were no modifications
            mockGateway.Verify();
		}

	    [Test]
		public void PopulateFromConfiguration()
		{
			publisher = EmailPublisherMother.Create();

			Assert.AreEqual("smtp.telus.net", publisher.MailHost);
			Assert.AreEqual("mailuser", publisher.MailhostUsername);
			Assert.AreEqual("mailpassword", publisher.MailhostPassword);
			Assert.AreEqual("ccnet@thoughtworks.com", publisher.FromAddress);

			Assert.AreEqual(5, publisher.EmailUsers.Count);
			ArrayList expected = new ArrayList();
			expected.Add(new EmailUser("buildmaster", "buildmaster", "servid@telus.net"));
			expected.Add(new EmailUser("orogers", "developers", "orogers@thoughtworks.com"));
			expected.Add(new EmailUser("manders", "developers", "mandersen@thoughtworks.com"));
			expected.Add(new EmailUser("dmercier", "developers", "dmercier@thoughtworks.com"));
			expected.Add(new EmailUser("rwan", "developers", "rwan@thoughtworks.com"));
			for (int i = 0; i < expected.Count; i++)
			{
				Assert.IsTrue(publisher.EmailUsers.ContainsValue(expected[i]));
			}

			Assert.AreEqual(2, publisher.EmailGroups.Count);
			EmailGroup developers = new EmailGroup("developers", EmailGroup.NotificationType.Change);
			EmailGroup buildmaster = new EmailGroup("buildmaster", EmailGroup.NotificationType.Always);
			Assert.AreEqual(developers, publisher.EmailGroups["developers"]);
			Assert.AreEqual(buildmaster, publisher.EmailGroups["buildmaster"]);
		}

		[Test]
		public void SerializeToXml()
		{
			publisher = EmailPublisherMother.Create();
			string xml = NetReflector.Write(publisher);
			XmlUtil.VerifyXmlIsWellFormed(xml);
		}

		[Test]
		public void VerifyEmailSubjectAndMessageForExceptionIntegrationResult()
		{
			IntegrationResult result = CreateIntegrationResult(IntegrationStatus.Exception, IntegrationStatus.Unknown);
			result.ExceptionResult = new CruiseControlException("test exception");

			Assert.IsTrue(publisher.CreateMessage(result).StartsWith("CruiseControl.NET Build Results for project Project#9"));

			publisher.IncludeDetails = true;
			string actual = publisher.CreateMessage(result);
			Assert.IsTrue(actual.IndexOf(result.ExceptionResult.Message) > 0);
			Assert.IsTrue(actual.IndexOf(result.ExceptionResult.GetType().Name) > 0);
			Assert.IsTrue(actual.IndexOf("BUILD COMPLETE") == -1); // verify build complete message is not output
		}

        private class MailMessageValidator : BaseConstraint
        {
            public override bool Eval(object val)
            {
                MailMessage message = (MailMessage)val;
                Assert.AreEqual("from@foo.com", message.From.Address);
                Assert.AreEqual("to@bar.com", message.To[0].Address);
                Assert.AreEqual("replyto@bar.com", message.ReplyTo.Address);
                Assert.AreEqual("test subject", message.Subject);
                Assert.AreEqual("test message", message.Body);
                return true;
            }

            public override string Message
            {
                get { return "MailMessage does not match!"; }
            }
        }

	    private class MailMessageRecipientValidator : BaseConstraint
	    {
	        private readonly int recipients;

	        public MailMessageRecipientValidator(int recipients)
	        {
	            this.recipients = recipients;
	        }

	        public override bool Eval(object val)
	        {
	            return recipients == ((MailMessage) val).To.Count;
	        }

	        public override string Message
	        {
	            get { return "Invalid number of recipients!"; }
	        }
	    }
	}
}