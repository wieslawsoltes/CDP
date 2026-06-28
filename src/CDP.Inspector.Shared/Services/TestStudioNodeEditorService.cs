#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Services;

public class TestStudioNodeEditorService
{
    private bool _isSyncing;

    public void SyncToTestStudio(
        TestStudioNodeEditorViewModel nodeEditor,
        ObservableCollection<TestStudioStepModel> steps,
        Action<TestStudioStepModel> subscribeStep,
        Action<TestStudioStepModel> unsubscribeStep,
        Action<bool> setUpdatingYaml,
        Action updateYaml)
    {
        if (_isSyncing) return;
        _isSyncing = true;
        try
        {
            nodeEditor.SyncSteps();
            var compiled = nodeEditor.CompiledSteps;

            setUpdatingYaml(true);
            try
            {
                foreach (var step in steps)
                {
                    unsubscribeStep(step);
                }

                steps.Clear();
                foreach (var step in compiled)
                {
                    subscribeStep(step);
                    steps.Add(step);
                }
            }
            finally
            {
                setUpdatingYaml(false);
            }

            updateYaml();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    public void SyncFromTestStudio(
        TestStudioNodeEditorViewModel nodeEditor,
        ObservableCollection<TestStudioStepModel> steps)
    {
        if (_isSyncing) return;
        _isSyncing = true;
        try
        {
            var currentNodes = nodeEditor.Nodes.OfType<TestStudioNodeViewModel>().ToList();
            var stepNodes = new List<TestStudioNodeViewModel>();
            var stepSet = new HashSet<TestStudioStepModel>(steps);

            foreach (var node in currentNodes)
            {
                if (node.Step == null || !stepSet.Contains(node.Step))
                {
                    nodeEditor.DeleteNode(node);
                }
            }

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var existingNode = nodeEditor.Nodes.OfType<TestStudioNodeViewModel>().FirstOrDefault(n => n.Step == step);
                if (existingNode == null)
                {
                    existingNode = nodeEditor.CreateNode(
                        $"Step {i + 1}",
                        step.Action,
                        step.Selector ?? "",
                        step.Value ?? "",
                        200.0 * i + 10.0,
                        20.0
                    );
                    existingNode.Step = step;
                }
                else
                {
                    existingNode.Name = $"Step {i + 1}";
                    existingNode.Action = step.Action;
                    existingNode.Selector = step.Selector ?? "";
                    existingNode.Value = step.Value ?? "";
                }
                stepNodes.Add(existingNode);
            }

            nodeEditor.Connections.Clear();
            if (stepNodes.Count > 0)
            {
                var prevNode = stepNodes[0];
                for (int i = 1; i < stepNodes.Count; i++)
                {
                    nodeEditor.ConnectNodes(prevNode, stepNodes[i]);
                    prevNode = stepNodes[i];
                }
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }
}
