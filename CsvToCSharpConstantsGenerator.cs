#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class CsvToCSharpConstantsGenerator : EditorWindow
{
	private TextAsset _CsvFile;
	private string _OutputFileNamePrefix = string.Empty;
	private string _TargetFolderPath = "Assets/";
	private string _GeneratedContentPreview = string.Empty;
	private List<string> _ProblematicKeys = new List<string>(); // 存储有问题的 Key
	private const char REPLACEMENT_CHAR = '_'; // 替换非法字符

	[MenuItem("Tool/CSV To C# Constants (Advanced)")]
	public static void ShowWindow()
	{
		GetWindow<CsvToCSharpConstantsGenerator>("CSV To C# Constants (Advanced)");
	}

	private void OnGUI()
	{
		GUILayout.Label("CSV 转 C# 常量工具 (高级)", EditorStyles.boldLabel);
		EditorGUILayout.Space();

		// 1. CSV 文件选择
		EditorGUILayout.HelpBox("请将您的 CSV 文件拖拽到下方区域。", MessageType.Info);
		_CsvFile = (TextAsset)EditorGUILayout.ObjectField("CSV 文件", _CsvFile, typeof(TextAsset), false);

		// --- 分隔线 ---
		EditorGUILayout.Space();
		GUILayout.Label("C# 文件输出设置", EditorStyles.boldLabel);
		EditorGUILayout.HelpBox("选择生成 C# 常量文件的目标文件夹，或直接拖拽一个文件夹到下方区域。", MessageType.Info);

		// 2. 目标文件夹选择 (按钮方式)
		if (GUILayout.Button("选择输出文件夹（方式一）"))
		{
			string selectedPath = EditorUtility.OpenFolderPanel("选择输出文件夹", _TargetFolderPath, "");
			if (!string.IsNullOrEmpty(selectedPath))
			{
				// 确保路径是相对于 Assets/ 的
				_TargetFolderPath = GetRelativePathFromAbsolutePath(selectedPath);
			}
		}
		EditorGUILayout.TextField("目标路径", _TargetFolderPath);

		// 2.5 拖拽文件夹区域
		Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true)); // 创建一个拖拽区域
		GUI.Box(dropArea, "拖拽文件夹到此（方式二）", EditorStyles.helpBox); // 用 HelpBox 样式绘制一个框

		HandleFolderDragAndDrop(dropArea); // 处理拖拽逻辑

		// --- 分隔线 ---

		// 3. 命名选项
		_OutputFileNamePrefix = EditorGUILayout.TextField("文件名后缀 (可选)", _OutputFileNamePrefix);
		EditorGUILayout.HelpBox("生成的 C# 文件名将是: CSV文件名 + 后缀 + .cs", MessageType.None);

		EditorGUILayout.Space(10);

		// 4. 生成按钮
		GUI.enabled = _CsvFile != null;
		if (GUILayout.Button("生成 C# 常量文件"))
		{
			GenerateCSharpConstantsFile();
		}
		GUI.enabled = true;

		// 5. 预览生成内容
		EditorGUILayout.Space(10);
		GUILayout.Label("生成内容预览:", EditorStyles.boldLabel);
		EditorGUILayout.SelectableLabel
		(
			_GeneratedContentPreview,
			EditorStyles.textArea,
			GUILayout.MinHeight(100),
			GUILayout.MaxHeight(200)
		);

		// 6. 问题 Key 列表
		EditorGUILayout.Space(10);
		GUILayout.Label("有问题或已清理的 Key:", EditorStyles.boldLabel);
		if (_ProblematicKeys.Count > 0)
		{
			StringBuilder sb = new StringBuilder();
			foreach (string key in _ProblematicKeys)
			{
				sb.AppendLine(key);
			}
			EditorGUILayout.SelectableLabel
			(
				sb.ToString(),
				EditorStyles.textArea,
				GUILayout.MinHeight(100),
				GUILayout.MaxHeight(200)
			);

			if (GUILayout.Button("导出问题 Key 到文件"))
			{
				ExportProblematicKeysToFile();
			}
		}
		else
		{
			EditorGUILayout.HelpBox("没有发现有问题的 Key。", MessageType.Info);
		}
	}

	private void ExportProblematicKeysToFile()
	{
		if (_ProblematicKeys.Count == 0)
		{
			EditorUtility.DisplayDialog("提示", "没有需要导出的问题 Key。", "确定");
			return;
		}

		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_CsvFile.name) + "_ProblematicKeys";
		string defaultFileName = (_CsvFile != null ? fileNameWithoutExtension : "ProblematicKeys") + ".txt";
		string filePath = EditorUtility.SaveFilePanel("保存问题 Key 列表", "", defaultFileName, "txt");

		if (!string.IsNullOrEmpty(filePath))
		{
			try
			{
				StringBuilder sb = new StringBuilder();
				foreach (string key in _ProblematicKeys)
				{
					sb.AppendLine(key);
				}
				File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
				Debug.Log($"问题 Key 列表已成功导出到: {filePath}");
				EditorUtility.DisplayDialog("导出成功", $"问题 Key 列表已成功导出到:\n{filePath}", "确定");
			}
			catch (Exception e)
			{
				Debug.LogError($"导出文件失败: {e.Message}");
				EditorUtility.DisplayDialog("导出失败", $"导出文件失败: {e.Message}", "确定");
			}
		}
		else
		{
			// 用户取消了保存操作
			Debug.Log("导出操作已取消。");
		}
	}

	private void HandleFolderDragAndDrop(Rect dropArea)
	{
		Event evt = Event.current; // 获取当前事件

		switch (evt.type)
		{
			case EventType.DragUpdated: // 拖拽进入区域时
			case EventType.DragPerform: // 拖拽并放下时
				if (!dropArea.Contains(evt.mousePosition)) // 如果鼠标不在拖拽区域内，不处理
					return;

				DragAndDrop.visualMode = DragAndDropVisualMode.Copy; // 显示拷贝图标

				if (evt.type == EventType.DragPerform) // 鼠标松开时
				{
					DragAndDrop.AcceptDrag(); // 接受拖拽操作

					foreach (Object draggedObject in DragAndDrop.objectReferences)
					{
						// 确保拖拽的是文件夹，并且在 Assets 目录下
						string path = AssetDatabase.GetAssetPath(draggedObject);
						if (AssetDatabase.IsValidFolder(path))
						{
							_TargetFolderPath = path + "/"; // 更新目标路径，确保以斜杠结尾
							Debug.Log($"目标文件夹已设置为: {_TargetFolderPath}");
							GUI.changed = true; // 告知 Unity GUI 状态已改变，进行重绘
							break; // 只需要第一个有效文件夹
						}
					}
				}
				evt.Use(); // 标记事件已使用，避免其他控件处理
				break;
		}
	}

	/// <summary>
	/// 将绝对路径转换为 Assets/ 相对路径。
	/// </summary>
	private string GetRelativePathFromAbsolutePath(string absolutePath)
	{
		if (absolutePath.StartsWith(Application.dataPath))
		{
			return "Assets/" + absolutePath.Substring(Application.dataPath.Length + 1) + "/";
		}
		else
		{
			EditorUtility.DisplayDialog("错误", "请选择 Assets/ 目录下的文件夹！", "确定");
			return _TargetFolderPath; // 返回原路径，或者默认 Assets/
		}
	}

	private void GenerateCSharpConstantsFile()
	{
		_ProblematicKeys.Clear(); // 清空上次运行的问题 Key 列表

		if (_CsvFile == null)
		{
			EditorUtility.DisplayDialog("错误", "请先拖拽一个 CSV 文件！", "确定");
			return;
		}

		string csvFileName = Path.GetFileNameWithoutExtension(_CsvFile.name);
		string csharpFileName = csvFileName + _OutputFileNamePrefix + ".cs";
		string fullOutputPath = Path.Combine(_TargetFolderPath, csharpFileName);

		if (File.Exists(fullOutputPath))
		{
			bool shouldOverwrite = EditorUtility.DisplayDialog(
				"文件已存在",
				$"目标文件 '{csharpFileName}' 已存在于 '{_TargetFolderPath}'。\n您确定要覆盖它吗？",
				"是，覆盖",
				"否，取消"
			);

			if (!shouldOverwrite)
			{
				Debug.Log($"用户取消了生成操作，文件 '{csharpFileName}' 未被覆盖。");
				EditorUtility.DisplayDialog("操作取消", "文件生成操作已取消。", "确定");
				return; // 用户选择不覆盖，直接返回
			}
		}

		string csvText = _CsvFile.text;
		StringReader reader = new StringReader(csvText);
		string line;
		bool isFirstLine = true;
		int lineNumber = 0; // 用于错误报告，指示行号

		StringBuilder classContent = new StringBuilder();
		classContent.AppendLine("// This file is auto-generated by CsvToCSharpConstantsGenerator (Advanced).");
		classContent.AppendLine("// Do not modify this file directly. Changes will be overwritten.");
		classContent.AppendLine("");
		classContent.AppendLine("public static class " + SanitizeClassName(csvFileName + _OutputFileNamePrefix));
		classContent.AppendLine("{");

		while ((line = reader.ReadLine()) != null)
		{
			lineNumber++;
			if (string.IsNullOrWhiteSpace(line)) continue;

			if (isFirstLine)
			{
				isFirstLine = false;
				continue;
			}

			string[] columns = line.Split(',');

			if (columns.Length > 0)
			{
				string originalKey = columns[0].Trim(); // 获取原始 Key 并去除前后空格

				if (string.IsNullOrEmpty(originalKey))
				{
					LogProblematicKey(originalKey, lineNumber, "Key 为空");
					continue; // 空 Key 不生成常量
				}

				// 尝试生成合法的常量名
				string constantName = SanitizeConstantName(originalKey);

				if (string.IsNullOrEmpty(constantName) || (!char.IsLetter(constantName[0]) && constantName[0] != '_'))
				{
					LogProblematicKey(originalKey, lineNumber, "无法生成合法 C# 字段名 (可能是空或以数字开头)");
					continue; // 无法生成合法名称的 Key 不生成常量
				}

				// 如果原始 Key 和清理后的常量名有明显区别，记录下来
				if (constantName != SanitizeNameForComparison(originalKey)) // SanitizeNameForComparison用于对比
				{
					_ProblematicKeys.Add($"第 {lineNumber} 行: 原始 Key '{originalKey}' 被清理为 '{constantName}'");
				}

				classContent.AppendLine($"    public const string {constantName} = \"{originalKey}\";");
			}
		}

		classContent.AppendLine("}");

		_GeneratedContentPreview = classContent.ToString();

		try
		{
			File.WriteAllText(fullOutputPath, classContent.ToString(), Encoding.UTF8);
			AssetDatabase.Refresh();
			Debug.Log($"成功生成 C# 常量文件: {fullOutputPath}");
			EditorUtility.DisplayDialog("成功", $"C# 常量文件已生成到:\n{fullOutputPath}\n\n详情请查看控制台和工具窗口中的“有问题或已清理的 Key”列表。",
				"确定");
			Object generatedAsset = AssetDatabase.LoadAssetAtPath<Object>(fullOutputPath);
			if (generatedAsset != null)
			{
				Selection.activeObject = generatedAsset;
			}
		}
		catch (Exception e)
		{
			Debug.LogError($"生成文件失败: {e.Message}");
			EditorUtility.DisplayDialog("错误", $"生成文件失败: {e.Message}", "确定");
		}
	}

	/// <summary>
	/// 清理字符串以作为合法的 C# 类名。
	/// 移除所有非法字符，并确保以字母或下划线开头。
	/// </summary>
	private string SanitizeClassName(string name)
	{
		// 移除所有非字母、数字、下划线的字符
		StringBuilder sb = new StringBuilder();
		foreach (char c in name)
		{
			if (char.IsLetterOrDigit(c) || c == '_')
			{
				sb.Append(c);
			}
		}
		string sanitized = sb.ToString();

		// 确保不是以数字开头
		if (sanitized.Length > 0 && char.IsDigit(sanitized[0]))
		{
			sanitized = "_" + sanitized;
		}

		// 避免空字符串
		if (string.IsNullOrEmpty(sanitized))
		{
			return "CsvConstants"; // 提供一个默认值
		}
		return sanitized;
	}

	/// <summary>
	/// 清理字符串以作为合法的 C# 常量名。
	/// 规则：
	/// 1. 将 Key 中一个或多个连续的空格替换为单个下划线。
	/// 2. 移除所有非法 C# 标识符字符。
	/// 3. 将剩余的合法字符转换为大写。
	/// 4. 确保不是以数字开头 (如果以数字开头，前缀加 '_')。
	/// 5. 移除开头和结尾的下划线。
	/// </summary>
	private string SanitizeConstantName(string name)
	{
		if (string.IsNullOrEmpty(name)) return "";

		// 1. 将 Key 中一个或多个连续的空格替换为单个下划线
		// 2. 移除所有非法 C# 标识符字符（除了字母、数字、下划线）
		StringBuilder sb = new StringBuilder();
		bool prevCharWasReplacement = false; // 用于控制连续下划线

		foreach (char c in name)
		{
			if (char.IsLetterOrDigit(c))
			{
				sb.Append(char.ToUpperInvariant(c)); // 转换为大写
				prevCharWasReplacement = false;
			}
			else if (char.IsWhiteSpace(c)) // 处理空格
			{
				if (!prevCharWasReplacement) // 如果前一个字符不是替换字符，就添加一个下划线
				{
					sb.Append(REPLACEMENT_CHAR);
					prevCharWasReplacement = true;
				}
			}
			else // 处理其他非法字符
			{
				if (!prevCharWasReplacement)
				{
					sb.Append(REPLACEMENT_CHAR);
					prevCharWasReplacement = true;
				}
			}
		}

		string sanitized = sb.ToString().Trim(REPLACEMENT_CHAR); // 移除开头和结尾的替换字符

		// 确保不是以数字开头
		if (sanitized.Length > 0 && char.IsDigit(sanitized[0]))
		{
			sanitized = REPLACEMENT_CHAR + sanitized;
		}

		// 如果清理后为空，表示无法生成合法名称
		if (string.IsNullOrEmpty(sanitized))
		{
			return ""; // 返回空字符串，表示生成失败
		}
		return sanitized;
	}

	/// <summary>
	/// 用于比较原始Key和清理后的常量名是否发生显著变化的辅助函数。
	/// 将原始Key做一次简单的清理，去除非法字符和连续空格，但不处理大小写。
	/// </summary>
	private string SanitizeNameForComparison(string name)
	{
		if (string.IsNullOrEmpty(name)) return "";
		StringBuilder sb = new StringBuilder();
		bool prevCharWasSpace = false;
		foreach (char c in name)
		{
			if (char.IsLetterOrDigit(c))
			{
				sb.Append(c);
				prevCharWasSpace = false;
			}
			else if (char.IsWhiteSpace(c))
			{
				if (!prevCharWasSpace)
				{
					sb.Append('_');
					prevCharWasSpace = true;
				}
			}
			else
			{
				// 对于比较，这里只保留字母数字和下划线
				if (!prevCharWasSpace)
				{
					sb.Append('_');
					prevCharWasSpace = true;
				}
			}
		}
		return sb.ToString().Trim('_');
	}

	/// <summary>
	/// 记录有问题的 Key 并添加到列表和控制台。
	/// </summary>
	private void LogProblematicKey(string originalKey, int lineNumber, string issue)
	{
		string logMessage = $"CSV 第 {lineNumber} 行: Key '{originalKey}' 存在问题: {issue}";
		Debug.LogError(logMessage);
		_ProblematicKeys.Add(logMessage);
	}
}
#endif
