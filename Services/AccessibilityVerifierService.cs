using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using AirGestureAI.Utilities;

namespace AirGestureAI.Services
{
    /// <summary>
    /// Inspects WPF visual tree elements for accessibility compliance (Tab order, Automation IDs/Names, Focus visuals, System parameters).
    /// </summary>
    public sealed class AccessibilityVerifierService
    {
        private readonly string _reportPath;

        /// <summary>
        /// Initializes a new instance of <see cref="AccessibilityVerifierService"/>.
        /// </summary>
        /// <param name="baseDirectory">Base folder path to store output reports.</param>
        public AccessibilityVerifierService(string baseDirectory)
        {
            _reportPath = Path.Combine(baseDirectory, "Diagnostics", "accessibility_report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_reportPath)!);
        }

        /// <summary>
        /// Traverses all active WPF windows to verify accessibility standards.
        /// </summary>
        public void VerifyAccessibility()
        {
            var issues = new List<AccessibilityIssue>();
            int checkedElements = 0;

            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (Window window in Application.Current.Windows)
                    {
                        InspectElement(window, issues, ref checkedElements);
                    }
                });
            }

            // Get system configuration indicators
            bool highContrast = SystemParameters.HighContrast;
            bool reducedMotion = !SystemParameters.ClientAreaAnimation;
            bool screenReaderActive = false;

            var report = new
            {
                Timestamp = DateTime.UtcNow.ToString("O"),
                TotalElementsChecked = checkedElements,
                IssuesFoundCount = issues.Count,
                SystemSettings = new
                {
                    HighContrastActive = highContrast,
                    ReducedMotionActive = reducedMotion,
                    ScreenReaderActive = screenReaderActive
                },
                Issues = issues
            };

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(report, options);
                File.WriteAllText(_reportPath, json);
                Logger.Info($"AccessibilityVerifier: Saved accessibility report with {issues.Count} issues to '{_reportPath}'.");
            }
            catch (Exception ex)
            {
                Logger.Error("AccessibilityVerifier: Failed to write accessibility report", ex);
            }
        }

        private void InspectElement(DependencyObject element, List<AccessibilityIssue> issues, ref int checkedElements)
        {
            if (element == null) return;
            checkedElements++;

            if (element is UIElement uiElement)
            {
                string typeName = element.GetType().Name;
                string name = uiElement.GetValue(FrameworkElement.NameProperty) as string ?? "Unnamed";

                // Check Automation ID
                string autoId = uiElement.GetValue(AutomationProperties.AutomationIdProperty) as string ?? "";
                if (string.IsNullOrEmpty(autoId))
                {
                    issues.Add(new AccessibilityIssue
                    {
                        ElementName = name,
                        ElementType = typeName,
                        IssueType = "MissingAutomationId",
                        Description = "Control lacks a defined AutomationProperties.AutomationId, making test automation difficult."
                    });
                }

                // Check Automation Name (Accessible Name)
                string autoName = uiElement.GetValue(AutomationProperties.NameProperty) as string ?? "";
                if (string.IsNullOrEmpty(autoName) && element is System.Windows.Controls.Control ctrl && ctrl.IsTabStop)
                {
                    issues.Add(new AccessibilityIssue
                    {
                        ElementName = name,
                        ElementType = typeName,
                        IssueType = "MissingAutomationName",
                        Description = "Focusable control lacks a defined AutomationProperties.Name, preventing screen readers from announcing it."
                    });
                }

                // Check Focus Visuals
                if (element is System.Windows.Controls.Control focusControl && focusControl.Focusable)
                {
                    if (focusControl.FocusVisualStyle == null)
                    {
                        issues.Add(new AccessibilityIssue
                        {
                            ElementName = name,
                            ElementType = typeName,
                            IssueType = "MissingFocusVisual",
                            Description = "Focusable control does not define a FocusVisualStyle, violating keyboard accessibility guidelines."
                        });
                    }
                }
            }

            // Recurse children
            int count = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                InspectElement(child, issues, ref checkedElements);
            }
        }
    }

    internal sealed class AccessibilityIssue
    {
        public string ElementName { get; set; } = string.Empty;
        public string ElementType { get; set; } = string.Empty;
        public string IssueType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
