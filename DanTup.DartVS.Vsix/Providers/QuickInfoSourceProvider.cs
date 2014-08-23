﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DanTup.DartAnalysis;
using DanTup.DartAnalysis.Json;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace DanTup.DartVS
{
	[Export(typeof(IQuickInfoSourceProvider))]
	[ContentType(DartContentTypeDefinition.DartContentType)]
	[Name("Dart Quick")]
	class QuickInfoSourceProvider : IQuickInfoSourceProvider
	{
		[Import]
		ITextDocumentFactoryService textDocumentFactory = null;

		[Import]
		DartAnalysisService analysisService = null;

		public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer buffer)
		{
			return new QuickInfoSource(this, buffer, textDocumentFactory, analysisService);
		}
	}

	class QuickInfoSource : IQuickInfoSource
	{
		QuickInfoSourceProvider provider;
		ITextBuffer buffer;
		ITextDocumentFactoryService textDocumentFactory;
		DartAnalysisService analysisService;

		public QuickInfoSource(QuickInfoSourceProvider provider, ITextBuffer buffer, ITextDocumentFactoryService textDocumentFactory, DartAnalysisService analysisService)
		{
			this.provider = provider;
			this.buffer = buffer;
			this.textDocumentFactory = textDocumentFactory;
			this.analysisService = analysisService;
		}

		int? inProgressPosition = null;
		string inProgressTooltipData = null;
		ITrackingSpan inProgressApplicableToSpan = null;


		public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
		{
			applicableToSpan = null;

			var triggerPoint = session.GetTriggerPoint(buffer.CurrentSnapshot);
			if (triggerPoint == null)
				return;

			ITextDocument doc;
			if (!textDocumentFactory.TryGetTextDocument(buffer, out doc))
				return;

			// Figure out if this is a recalculate for an existing span (not sure if this is the best way of supporting async...?)
			if (inProgressPosition != null && inProgressPosition.Value == triggerPoint.Value.Position)
				UpdateTooltip(session, quickInfoContent, out applicableToSpan);
			else
				StartTooltipRequest(session, quickInfoContent, out applicableToSpan, triggerPoint, doc.FilePath);
		}

		void StartTooltipRequest(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan, SnapshotPoint? triggerPoint, string filePath)
		{
			// Set the position so we know what request is in process.
			inProgressPosition = triggerPoint.Value.Position;
			inProgressTooltipData = null;
			inProgressApplicableToSpan = null;

			// Put dummy content in tooltip while the request in in-flight.
			quickInfoContent.Add("Loading...");
			// Attempt to create a span 1 char left and 1 char right; but within bounds of buffer (otherwise crashes).
			var start = Math.Max(triggerPoint.Value.Position - 1, 0);
			var length = Math.Min(triggerPoint.Value.Position + 1, buffer.CurrentSnapshot.Length) - start;
			applicableToSpan = buffer.CurrentSnapshot.CreateTrackingSpan(start, length, SpanTrackingMode.EdgeInclusive);

			// Fire off a request to the service to get the data.
			var hoverTask = analysisService.GetHover(filePath, triggerPoint.Value.Position); // Can't await, not-async method :(
			hoverTask.ContinueWith(hovers =>
			{
				// Build the tooltip info if the response was valid.
				var tooltipData = BuildTooltip(hovers.Result);

				if (!string.IsNullOrWhiteSpace(tooltipData))
				{
					// Stash the data for the next call, and tell VS to reclaculate now that we have the good info.
					inProgressTooltipData = tooltipData;
					inProgressApplicableToSpan = buffer.CurrentSnapshot.CreateTrackingSpan(hovers.Result[0].Offset, hovers.Result[0].Length, SpanTrackingMode.EdgeInclusive);
					session.Recalculate();
				}
				else
					// Otherwise, no valid response, means no tooltip.
					session.Dismiss();
			}, TaskScheduler.FromCurrentSynchronizationContext()); // TODO: Without this, Dismiss doesn't work; but is this a good way to do it?
		}

		void UpdateTooltip(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
		{
			// Provide the tooltip data from the data we stashed in our callback.
			quickInfoContent.Add(inProgressTooltipData);
			// TODO: For some reason, this doesn't seem to work properly; the tooltip flickers as the mouse moves (and fires off additional requests) :(
			applicableToSpan = inProgressApplicableToSpan;
		}

		string BuildTooltip(HoverInformation[] hovers)
		{
			if (hovers == null || hovers.Length == 0 || hovers[0] == null)
				return null;

			var typeInfo = hovers[0].ElementDescription ?? hovers[0].Parameter;

			return string.Format("{0}\r\n{1}", typeInfo, hovers[0].Dartdoc).Trim();
		}

		public void Dispose()
		{
			GC.SuppressFinalize(true);
		}
	}
}
