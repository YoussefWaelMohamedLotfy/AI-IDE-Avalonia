using System.Collections.ObjectModel;

namespace AI_IDE_Avalonia.Models;

public class TreeNode
{
    public string Name { get; }
    public bool IsFolder { get; }
    public ObservableCollection<TreeNode>? Children { get; }

    public TreeNode(string name, bool isFolder = false, ObservableCollection<TreeNode>? children = null)
    {
        Name = name;
        IsFolder = isFolder;
        Children = children;
    }

    private static ObservableCollection<TreeNode> Nodes(params TreeNode[] nodes) =>
        new(nodes);

    public static ObservableCollection<TreeNode> CreateSampleProject() =>
        Nodes(
            new TreeNode("MyAIProject", isFolder: true, children: Nodes(
                new TreeNode("src", isFolder: true, children: Nodes(
                    new TreeNode("Agents", isFolder: true, children: Nodes(
                        new TreeNode("ChatAgent.cs"),
                        new TreeNode("SummaryAgent.cs"),
                        new TreeNode("CodeAgent.cs")
                    )),
                    new TreeNode("Models", isFolder: true, children: Nodes(
                        new TreeNode("ChatMessage.cs"),
                        new TreeNode("ConversationHistory.cs"),
                        new TreeNode("ModelSettings.cs")
                    )),
                    new TreeNode("Services", isFolder: true, children: Nodes(
                        new TreeNode("OpenAIService.cs"),
                        new TreeNode("AnthropicService.cs"),
                        new TreeNode("IModelService.cs")
                    )),
                    new TreeNode("Prompts", isFolder: true, children: Nodes(
                        new TreeNode("SystemPrompt.txt"),
                        new TreeNode("CodeReviewPrompt.txt")
                    ))
                )),
                new TreeNode("tests", isFolder: true, children: Nodes(
                    new TreeNode("AgentTests.cs"),
                    new TreeNode("ServiceTests.cs")
                )),
                new TreeNode("README.md"),
                new TreeNode("MyAIProject.csproj")
            ))
        );
}
