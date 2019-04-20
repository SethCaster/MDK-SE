﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Malware.MDKServices;
using MDK.Resources;
using MDK.Views.ProjectHealth.Fixes;

namespace MDK.Views.ProjectHealth
{
    /// <summary>
    /// The view model for the <see cref="ProjectHealthDialog"/> view.
    /// </summary>
    public class ProjectHealthDialogModel : DialogViewModel
    {
        bool _isUpgrading;
        bool _isCompleted;
        string _message = Text.ProjectHealthDialogModel_DefaultMessage;
        ObservableCollection<FixStatus> _fixStatuses = new ObservableCollection<FixStatus>();
        readonly List<Fix> _fixes = new Fix[]
        {
            new BackupFix(),
            new OutdatedFix(),
            new BadInstallPathFix(),
            new MissingPathsFileFix(),
            new MissingWhitelistFix(),
            new BadGamePathFix(), 
            new BadOutputPathFix()
        }.OrderBy(f => f.SortIndex).ToList();

        string _okText = "Repair";

        /// <summary>
        /// Creates a new instance of this view model.
        /// </summary>
        /// <param name="package"></param>
        /// <param name="analyses"></param>
        public ProjectHealthDialogModel([NotNull] MDKPackage package, [NotNull] HealthAnalysis[] analyses)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));

            Projects = analyses.ToList().AsReadOnly();

            FixStatuses = new ReadOnlyObservableCollection<FixStatus>(_fixStatuses);
        }

        /// <summary>
        /// A list of projects and their problems
        /// </summary>
        public ReadOnlyCollection<HealthAnalysis> Projects { get; set; }

        /// <summary>
        /// A list of in progress or completed fix statuses
        /// </summary>
        public ReadOnlyObservableCollection<FixStatus> FixStatuses { get; }

        /// <summary>
        /// The text that represents the OK button
        /// </summary>
        public string OkText
        {
            get => _okText;
            set
            {
                if (value == _okText)
                    return;
                _okText = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Whether the dialog is busy upgrading projects
        /// </summary>
        public bool IsUpgrading
        {
            get => _isUpgrading;
            private set
            {
                if (value == _isUpgrading)
                    return;
                _isUpgrading = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Contains the message to display in the dialog.
        /// </summary>
        public string Message
        {
            get => _message;
            set
            {
                if (value == _message)
                    return;
                _message = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// The associated MDK package
        /// </summary>
        public MDKPackage Package { get; }

        /// <inheritdoc />
        protected override bool OnCancel()
        {
            if (!_isCompleted)
            {
                foreach (var analysis in Projects)
                {
                    if (analysis.Problems.Any(p => p.Severity == HealthSeverity.Critical))
                        analysis.Project.Unload();
                }
            }

            return base.OnCancel();
        }

        /// <summary>
        /// Upgrades the projects.
        /// </summary>
        protected override bool OnSave()
        {
            if (!SaveAndCloseCommand.IsEnabled)
                return false;
            if (!_isCompleted)
                RunUpgrades();  
            return _isCompleted;
        }

        async void RunUpgrades()
        {
            try
            {
                SaveAndCloseCommand.IsEnabled = false;
                CancelCommand.IsEnabled = false;
                IsUpgrading = true;
                foreach (var project in Projects)
                {
                    var handle = project.Project.Unload();
                    var fixes = _fixes.Where(f => f.IsApplicableTo(project));
                    foreach (var fix in fixes)
                    {
                        var status = new FixStatus();
                        _fixStatuses.Add(status);
                        await Task.Run(() => fix.Apply(project, status));
                    }

                    handle.Reload();
                }

                _isCompleted = true;
                OkText = "Close";
                SaveAndCloseCommand.IsEnabled = true;
            }
            catch (Exception e)
            {
                Package.ShowError(Text.ProjectHealthDialogModel_OnSave_Error, Text.ProjectHealthDialogModel_OnSave_Error_Description, e);
            }
        }
    }
}
