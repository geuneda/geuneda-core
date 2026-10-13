using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Geuneda.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace Geuneda.Core.Tests
{
	[TestFixture]
	public class ModuleLoggerTest
	{
		private sealed class RecordingSink : ILogSink
		{
			public readonly List<LogEntry> Entries = new List<LogEntry>();

			public void Write(in LogEntry entry)
			{
				Entries.Add(entry);
			}
		}

		private sealed class ThrowingSink : ILogSink
		{
			public void Write(in LogEntry entry)
			{
				throw new InvalidOperationException("sink failed");
			}
		}

		private ILogSink _originalSink;
		private RecordingSink _sink;
		private ModuleLogger _logger;

		[SetUp]
		public void SetUp()
		{
			_originalSink = Log.Sink;
			_sink = new RecordingSink();
			Log.Sink = _sink;
			_logger = new ModuleLogger("Test.Module");
		}

		[TearDown]
		public void TearDown()
		{
			Log.Sink = _originalSink;
		}

		private static int CurrentLine([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
		{
			return line;
		}

		[Test]
		public void NewLogger_VerboseAndDebugOff_OthersOn()
		{
			Assert.IsFalse(_logger.IsEnabled(LogLevel.Verbose));
			Assert.IsFalse(_logger.IsEnabled(LogLevel.Debug));
			Assert.IsTrue(_logger.IsEnabled(LogLevel.Info));
			Assert.IsTrue(_logger.IsEnabled(LogLevel.Warning));
			Assert.IsTrue(_logger.IsEnabled(LogLevel.Error));
			Assert.IsTrue(_logger.IsEnabled(LogLevel.Fatal));
		}

		[Test]
		public void Info_WritesEntryWithModuleMessageAndCaller()
		{
			_logger.Info("hello"); var expectedLine = CurrentLine();

			Assert.AreEqual(1, _sink.Entries.Count);
			var entry = _sink.Entries[0];
			Assert.AreEqual(LogLevel.Info, entry.Level);
			Assert.AreEqual("Test.Module", entry.Module);
			Assert.AreEqual("hello", entry.Message);
			Assert.IsNull(entry.Exception);
			Assert.AreEqual(nameof(Info_WritesEntryWithModuleMessageAndCaller), entry.CallerMember);
			StringAssert.EndsWith("ModuleLoggerTest.cs", entry.CallerFile);
			Assert.AreEqual(expectedLine, entry.CallerLine);
		}

		[Test]
		public void DisabledLevels_AreNotWritten()
		{
			_logger.Verbose("verbose");
			_logger.Debug("debug");
			_logger.Disable(LogLevel.Info);
			_logger.Info("info");

			CollectionAssert.IsEmpty(_sink.Entries);
		}

		[Test]
		public void EnabledVerboseAndDebug_AreWrittenInEditor()
		{
			_logger.Enable(LogLevel.Verbose | LogLevel.Debug);

			_logger.Verbose("verbose");
			_logger.Debug("debug");

			Assert.AreEqual(2, _sink.Entries.Count);
			Assert.AreEqual(LogLevel.Verbose, _sink.Entries[0].Level);
			Assert.AreEqual(LogLevel.Debug, _sink.Entries[1].Level);
		}

		[Test]
		public void EnabledLevels_CanBeReplaced()
		{
			_logger.EnabledLevels = LogLevel.Error;

			_logger.Warning("warning");
			_logger.Error("error");

			Assert.AreEqual(1, _sink.Entries.Count);
			Assert.AreEqual(LogLevel.Error, _sink.Entries[0].Level);
		}

		[Test]
		public void ErrorWithException_UsesExceptionMessageWhenNoMessage()
		{
			var exception = new InvalidOperationException("broken");

			_logger.Error(exception);
			_logger.Error(exception, "custom");

			Assert.AreSame(exception, _sink.Entries[0].Exception);
			Assert.AreEqual("broken", _sink.Entries[0].Message);
			Assert.AreEqual("custom", _sink.Entries[1].Message);
		}

		[Test]
		public void Fatal_WritesEntryAndReturnsSameException()
		{
			var exception = new ArgumentException("bad argument");

			var thrown = Assert.Throws<ArgumentException>(() => throw _logger.Fatal(exception));

			Assert.AreSame(exception, thrown);
			Assert.AreEqual(1, _sink.Entries.Count);
			Assert.AreEqual(LogLevel.Fatal, _sink.Entries[0].Level);
			Assert.AreSame(exception, _sink.Entries[0].Exception);
		}

		[Test]
		public void NullException_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => _logger.Error((Exception)null));
			Assert.Throws<ArgumentNullException>(() => _logger.Fatal<Exception>(null));
		}

		[Test]
		public void EmptyModuleName_Throws()
		{
			Assert.Throws<ArgumentException>(() => new ModuleLogger(null));
			Assert.Throws<ArgumentException>(() => new ModuleLogger(""));
		}

		[Test]
		public void SinkThrows_DoesNotPropagateToCaller()
		{
			Log.Sink = new ThrowingSink();

			LogAssert.Expect(LogType.Exception, new Regex("sink failed"));

			Assert.DoesNotThrow(() => _logger.Info("hello"));
		}

		[Test]
		public void SetSinkNull_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => Log.Sink = null);
		}

		[Test]
		public void SubsystemRegistrationReset_RestoresConsoleSink()
		{
			var method = typeof(Log).GetMethod("ResetOnSubsystemRegistration", BindingFlags.NonPublic | BindingFlags.Static);
			Assert.IsNotNull(method);

			var attribute = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
			Assert.IsNotNull(attribute);
			Assert.AreEqual(RuntimeInitializeLoadType.SubsystemRegistration, attribute.loadType);

			method.Invoke(null, null);

			Assert.IsInstanceOf<UnityConsoleSink>(Log.Sink);
		}

		[Test]
		public void SubsystemRegistrationReset_RestoresLevelsOfExistingLogger()
		{
			_logger.EnabledLevels = LogLevel.None;
			var method = typeof(Log).GetMethod("ResetOnSubsystemRegistration", BindingFlags.NonPublic | BindingFlags.Static);

			method.Invoke(null, null);

			Assert.AreEqual(ModuleLogger.DefaultLevels, _logger.EnabledLevels);
		}

		[Test]
		public void EnableFromManyThreads_KeepsEveryLevel()
		{
			var levels = new[] { LogLevel.Verbose, LogLevel.Debug, LogLevel.Info, LogLevel.Warning, LogLevel.Error, LogLevel.Fatal };

			for (var iteration = 0; iteration < 500; iteration++)
			{
				_logger.EnabledLevels = LogLevel.None;

				Parallel.ForEach(levels, level => _logger.Enable(level));

				Assert.AreEqual(LogLevel.All, _logger.EnabledLevels, $"iteration {iteration}");
			}
		}

		[Test]
		public void DisableFromManyThreads_RemovesEveryLevel()
		{
			var levels = new[] { LogLevel.Verbose, LogLevel.Debug, LogLevel.Info, LogLevel.Warning, LogLevel.Error, LogLevel.Fatal };

			for (var iteration = 0; iteration < 500; iteration++)
			{
				_logger.EnabledLevels = LogLevel.All;

				Parallel.ForEach(levels, level => _logger.Disable(level));

				Assert.AreEqual(LogLevel.None, _logger.EnabledLevels, $"iteration {iteration}");
			}
		}

		[Test]
		public void UnityConsoleSink_WritesLevelAsLogTypeWithModulePrefix()
		{
			Log.Sink = new UnityConsoleSink();

			LogAssert.Expect(LogType.Log, "[Test.Module] info {0}");
			_logger.Info("info {0}");

			LogAssert.Expect(LogType.Warning, "[Test.Module] warning");
			_logger.Warning("warning");

			LogAssert.Expect(LogType.Error, "[Test.Module] error");
			_logger.Error("error");

			LogAssert.Expect(LogType.Error, "[Test.Module] fatal");
			LogAssert.Expect(LogType.Exception, new Regex("fatal"));
			_logger.Fatal(new InvalidOperationException("fatal"));
		}
	}
}
