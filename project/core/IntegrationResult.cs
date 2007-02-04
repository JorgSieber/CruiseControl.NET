using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using ThoughtWorks.CruiseControl.Core.Tasks;
using ThoughtWorks.CruiseControl.Core.Util;
using ThoughtWorks.CruiseControl.Remote;

namespace ThoughtWorks.CruiseControl.Core
{
	/// <summary>
	/// Contains all the results of a project's integration.
	/// </summary>
	[Serializable]
	public class IntegrationResult : IIntegrationResult
	{
		public const string InitialLabel = "UNKNOWN";

		// immutable properties
		private string projectName;
		private string projectUrl;
		private string workingDirectory;
		private string artifactDirectory;
		private IntegrationRequest request;
		private BuildCondition buildCondition;

		// mutable properties
		private IntegrationStatus status = IntegrationStatus.Unknown;
		private string label = InitialLabel;
		private DateTime startTime;
		private DateTime endTime;
		private Modification[] modifications = new Modification[0];
		private Exception exception;
		private ArrayList taskResults = new ArrayList();

		// previous result properties
		private IntegrationStatus lastIntegrationStatus;
		private string lastSuccessfulIntegrationLabel;

		// Default constructor required for serialization
		public IntegrationResult()
		{
			BuildCondition = BuildCondition.NoBuild;
			LastIntegrationStatus = IntegrationStatus.Unknown;
		}

		public IntegrationResult(string projectName, string workingDirectory) : this()
		{
			ProjectName = projectName;
			WorkingDirectory = workingDirectory;
		}

		public IntegrationResult(string projectName, string workingDirectory, IntegrationRequest request) : this(projectName, workingDirectory)
		{
			this.request = request;
			BuildCondition = request.BuildCondition;
		}

		public string ProjectName
		{
			get { return projectName; }
			set { projectName = value; }
		}

		public string ProjectUrl
		{
			get { return projectUrl; }
			set { projectUrl = value; }
		}

		public BuildCondition BuildCondition
		{
			get { return buildCondition; }
			set { buildCondition = value; }
		}

		public string Label
		{
			get { return label; }
			set { label = value; }
		}

		//
		// If you have a label that can be represented in a simple numeric form,
		// then this returns it.  If you don't, then this returns "0".
		// NOTE: "0" is better than "-1" since build numbers are non-negative
		// and "-" is a character frequently used to separate version components
		// when represented in string form.  Thus "-1" might give someone
		// "1-0--1", which might cause all sorts of havoc for them.  Best to
		// avoid the "-" character.
		//
		public int NumericLabel
		{
			get
			{
				try
				{
					string tempNumericLabel = Regex.Replace(Label, @".*?(\d+$)", "$1");
					return int.Parse(tempNumericLabel);
				}
				catch (FormatException)
				{
					return 0;
				}
			}
		}

		public string WorkingDirectory
		{
			get { return workingDirectory; }
			set { workingDirectory = value; }
		}

		public string ArtifactDirectory
		{
			get { return artifactDirectory; }
			set { artifactDirectory = value; }
		}

		public string IntegrationArtifactDirectory
		{
			get { return Path.Combine(ArtifactDirectory, Label); }
		}

		public IntegrationStatus Status
		{
			get { return status; }
			set { status = value; }
		}

		public IntegrationStatus LastIntegrationStatus
		{
			get { return lastIntegrationStatus; }
			set { lastIntegrationStatus = value; }
		}

		public string LastSuccessfulIntegrationLabel
		{
			get { return (Succeeded || lastSuccessfulIntegrationLabel == null) ? Label : lastSuccessfulIntegrationLabel; }
			set { lastSuccessfulIntegrationLabel = value; }
		}

		public string PreviousLabel
		{
			get { return lastSuccessfulIntegrationLabel; }
		}

		/// <summary>
		/// Gets and sets the date and time at which the integration commenced.
		/// </summary>
		public DateTime StartTime
		{
			get { return startTime; }
			set { startTime = value; }
		}

		/// <summary>
		/// Gets and sets the date and time at which the integration was completed.
		/// </summary>
		public DateTime EndTime
		{
			get { return endTime; }
			set { endTime = value; }
		}

		[XmlIgnore]
		public virtual Modification[] Modifications
		{
			get { return modifications; }
			set { modifications = value; }
		}

		public DateTime LastModificationDate
		{
			get
			{
				//TODO: why set the date to yesterday's date as a default
				if (Modifications.Length == 0)
				{
					//If there are no modifications then this should be set to the last modification date
					// from the last integration (or 1/1/1980 if there is no previous integration).
					return DateTime.Now.AddDays(-1.0);
				}

				DateTime latestDate = DateTime.MinValue;
				foreach (Modification modification in Modifications)
				{
					latestDate = DateUtil.MaxDate(modification.ModifiedTime, latestDate);
				}
				return latestDate;
			}
		}

		public int LastChangeNumber
		{
			get
			{
				int lastChangeNumber = 0;
				foreach (Modification modification in modifications)
				{
					if (modification.ChangeNumber > lastChangeNumber)
						lastChangeNumber = modification.ChangeNumber;
				}
				return lastChangeNumber;
			}
		}

		public bool IsInitial()
		{
			return Label == InitialLabel;
		}

		/// <summary>
		/// Gets a value indicating the success of this integration.
		/// </summary>
		public bool Succeeded
		{
			get { return Status == IntegrationStatus.Success; }
		}

		/// <summary>
		/// Gets a value indicating whether this integration failed.
		/// </summary>
		public bool Failed
		{
			get { return Status == IntegrationStatus.Failure; }
		}

		/// <summary>
		/// Gets a value indicating whether this integration fixed a previously broken build.
		/// </summary>
		public bool Fixed
		{
			get { return Succeeded && LastIntegrationStatus == IntegrationStatus.Failure; }
		}

		/// <summary>
		/// Gets the time taken to perform the project's integration.
		/// </summary>
		public TimeSpan TotalIntegrationTime
		{
			get { return EndTime - StartTime; }
		}

		// Exceptions cannot be serialised because of permission attributes
		[XmlIgnore]
		public Exception ExceptionResult
		{
			get { return exception; }
			set
			{
				exception = value;
				if (exception != null)
				{
					Status = IntegrationStatus.Exception;
				}
			}
		}

		[XmlIgnore]
		public IList TaskResults
		{
			get { return taskResults; }
		}

		public void AddTaskResult(string result)
		{
			AddTaskResult(new DataTaskResult(result));
		}

		public void AddTaskResult(ITaskResult result)
		{
			taskResults.Add(result);
			if (! (Failed || Status == IntegrationStatus.Exception)) Status = result.Succeeded() ? IntegrationStatus.Success : IntegrationStatus.Failure;
		}

		public void MarkStartTime()
		{
			StartTime = DateTime.Now;
		}

		public void MarkEndTime()
		{
			EndTime = DateTime.Now;
		}

		public bool HasModifications()
		{
			return Modifications.Length > 0;
		}

		public override bool Equals(object obj)
		{
			IntegrationResult other = obj as IntegrationResult;
			if (other == null)
			{
				return false;
			}
			return ProjectName == other.ProjectName &&
			       Status == other.Status &&
			       Label == other.Label &&
			       StartTime == other.StartTime;
		}

		public override int GetHashCode()
		{
			return (ProjectName + Label + StartTime.Ticks).GetHashCode();
		}

		public static IntegrationResult CreateInitialIntegrationResult(string project, string workingDirectory)
		{
			IntegrationResult result = new IntegrationResult(project, workingDirectory);
			result.StartTime = DateTime.Now.AddDays(-1);
			result.EndTime = DateTime.Now;
			result.BuildCondition = BuildCondition.ForceBuild;
			return result;
		}

		/// <summary>
		/// Determines whether a build should run.  A build should run if there
		/// are modifications, and none have occurred within the modification
		/// delay.
		/// </summary>
		public bool ShouldRunBuild()
		{
			return BuildCondition.ForceBuild == BuildCondition || HasModifications();
		}

		public string BaseFromArtifactsDirectory(string pathToBase)
		{
			if (StringUtil.IsBlank(pathToBase)) return ArtifactDirectory;
			return Path.Combine(ArtifactDirectory, pathToBase);
		}

		public string BaseFromWorkingDirectory(string pathToBase)
		{
			if (StringUtil.IsBlank(pathToBase)) return WorkingDirectory;
			return Path.Combine(WorkingDirectory, pathToBase);
		}

		/// <summary>
		/// Contains the output from the build process.  In the case of NAntBuilder, this is the 
		/// redirected StdOut of the nant.exe process.
		/// </summary>
		[XmlIgnore]
		public string TaskOutput
		{
			get
			{
				StringBuilder builder = new StringBuilder();
				foreach (ITaskResult result in taskResults)
				{
					builder.Append(result.Data);
				}
				return builder.ToString();
			}
		}

		[XmlIgnore]
		public IntegrationSummary LastIntegration
		{
			get { return new IntegrationSummary(LastIntegrationStatus, PreviousLabel); }
		}

		[XmlIgnore]
		public IntegrationRequest IntegrationRequest
		{
			get { return request; }
		}

		[XmlIgnore]
		public IDictionary IntegrationProperties
		{
			get
			{
				IDictionary fullProps = new SortedList();
				fullProps["CCNetProject"] = projectName;
				if (projectUrl != null) fullProps["CCNetProjectUrl"] = projectUrl;
				fullProps["CCNetWorkingDirectory"] = workingDirectory;
				fullProps["CCNetArtifactDirectory"] = artifactDirectory;
				fullProps["CCNetIntegrationStatus"] = Status;
				fullProps["CCNetLabel"] = Label;
				fullProps["CCNetBuildCondition"] = BuildCondition;
				fullProps["CCNetNumericLabel"] = NumericLabel;
				fullProps["CCNetBuildDate"] = StartTime.ToString("yyyy-MM-dd", null);
				fullProps["CCNetBuildTime"] = StartTime.ToString("HH:mm:ss", null);
				fullProps["CCNetLastIntegrationStatus"] = LastIntegrationStatus;
				if (IntegrationRequest != null) fullProps["CCNetRequestSource"] = IntegrationRequest.Source;
				return fullProps;
			}
		}

		public override string ToString()
		{
			return string.Format("Project: {0}, Status: {1}, Label: {2}, StartTime: {3}", ProjectName, Status, Label, StartTime);
		}
	}

	public class IntegrationSummary
	{
		private IntegrationStatus status;
		private string label;

		public IntegrationSummary(IntegrationStatus status, string label)
		{
			this.status = status;
			this.label = label;
		}

		public override bool Equals(object obj)
		{
			if (obj == null) return false;
			if (obj.GetType() != GetType()) return false;

			IntegrationSummary other = (IntegrationSummary) obj;
			return other.status.Equals(status) && other.label == label;
		}

		public override int GetHashCode()
		{
			return label.GetHashCode();
		}

		public string Label
		{
			get { return label; }
		}

		public IntegrationStatus Status
		{
			get { return status; }
		}
	}
}