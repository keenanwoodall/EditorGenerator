using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.CodeDom;
using System.CodeDom.Compiler;
using UnityEngine;
using UnityEditor;

namespace Beans.Unity.Editor.EditorGenerator
{
	public class EditorGenerator
	{
		private string path = "Assets";
		private string editorTypeName;

		private MonoScript script;
		private CodeCompileUnit unitCode;

		private IEnumerable<FieldInfo> serializedFields;

		public static bool IsValidMonoScript (MonoScript script)
		{
			if (script == null)
				return false;

			var scriptClass = script.GetClass ();
			return scriptClass.IsSubclassOf (typeof (MonoBehaviour)) || scriptClass.IsSubclassOf (typeof (ScriptableObject));
		}

		public void Create (MonoScript script)
		{
			this.script = script ?? throw new ArgumentNullException ("Script cannot be null.");

			var scriptClass = script.GetClass ();
			if (!IsValidMonoScript (script))
				throw new ArgumentException ("Script must derive from MonoBehaviour or ScriptableObject.");

			unitCode = new CodeCompileUnit ();

			serializedFields = GetSerializedFields (script);

			var namespaceCode = CreateNamespaceCode (script);

			namespaceCode.Types.Add (CreateClassCode (script));
			unitCode.Namespaces.Add (namespaceCode);
		}

		public MonoScript Save ()
		{
			path = EditorUtility.SaveFilePanelInProject ("Save script", $"{script.name}Editor", "cs", "", path);

			if (string.IsNullOrEmpty (path))
				return null;

			var oldScript = (MonoScript)AssetDatabase.LoadAssetAtPath (path, typeof (MonoScript));
			if (oldScript == null)
			{
				AssetDatabase.DeleteAsset (path);
				AssetDatabase.Refresh (ImportAssetOptions.ForceSynchronousImport);
			}

			var provider = CodeDomProvider.CreateProvider ("CSharp");
			var options = new CodeGeneratorOptions ()
			{
				BracingStyle = "C"
			};

			using (var writer = new StreamWriter (path))
				provider.GenerateCodeFromCompileUnit (unitCode, writer, options);

			AssetDatabase.Refresh (ImportAssetOptions.ForceUpdate);

			return (MonoScript)AssetDatabase.LoadAssetAtPath (path, typeof (MonoScript));
		}

		private CodeNamespace CreateNamespaceCode (MonoScript script)
		{
			var namespaceCode = new CodeNamespace ();

			// Set name
			var name = script.GetClass ().Namespace;
			if (!string.IsNullOrEmpty (name))
				namespaceCode.Name = $"{name}Editor";

			namespaceCode.Imports.Add (new CodeNamespaceImport ("UnityEditor"));

			return namespaceCode;
		}

		private CodeTypeDeclaration CreateClassCode (MonoScript script)
		{
			editorTypeName = script.name;
			var classCode = new CodeTypeDeclaration ($"{editorTypeName}Editor");

			// Make class inherit from Editor
			classCode.BaseTypes.Add ("Editor");

			// Make unit a public class
			classCode.IsClass = true;
			classCode.TypeAttributes = TypeAttributes.Public;

			// Create all the code for the class
			CreateAttributes (classCode);
			CreateFields (classCode, script);
			CreateMethods (classCode);

			return classCode;
		}

		private void CreateAttributes (CodeTypeDeclaration classCode)
		{
			var canEditMultipleObjectsAttributeCode = new CodeAttributeDeclaration (new CodeTypeReference (typeof (CanEditMultipleObjects)));
			var customEditorAttributeCode = new CodeAttributeDeclaration (new CodeTypeReference (typeof (CustomEditor)));
			customEditorAttributeCode.Arguments.Add (new CodeAttributeArgument (new CodeTypeOfExpression (script.GetClass ())));

			classCode.CustomAttributes.Add (canEditMultipleObjectsAttributeCode);
			classCode.CustomAttributes.Add (customEditorAttributeCode);
		}

		private void CreateFields (CodeTypeDeclaration classCode, MonoScript script)
		{
			foreach (var field in serializedFields)
			{
				var newFieldCode = new CodeMemberField ();

				newFieldCode.Type = new CodeTypeReference (typeof (GUIContent));
				newFieldCode.Name = $"{field.Name}Content";

				var tooltip = (TooltipAttribute)field.GetCustomAttributes ().FirstOrDefault (a => a is TooltipAttribute);

				var parameters = new CodeExpression[tooltip == null ? 1 : 2];

				parameters[0] = new CodePrimitiveExpression (ObjectNames.NicifyVariableName (field.Name));
				if (tooltip != null)
					parameters[1] = new CodePrimitiveExpression (tooltip.tooltip);

				newFieldCode.InitExpression = new CodeObjectCreateExpression 
				(
					createType: typeof (GUIContent),
					parameters: parameters
				);

				classCode.Members.Add (newFieldCode);
			}
		}

		private void CreateMethods (CodeTypeDeclaration classCode)
		{
			classCode.Members.Add (CreateOnInspectorGUIMethod ());
		}

		private CodeMemberMethod CreateOnInspectorGUIMethod ()
		{
			var inspectorGUICode = new CodeMemberMethod ()
			{
				Name = "OnInspectorGUI",
				Attributes = MemberAttributes.Public | MemberAttributes.Override
			};

			// UpdateIfRequiredOrScript
			inspectorGUICode.Statements.Add (new CodeMethodInvokeExpression (new CodeMethodReferenceExpression (new CodeVariableReferenceExpression ("serializedObject"), "UpdateIfRequiredOrScript")));

			// Property Fields
			foreach (var field in serializedFields)
			{
				var attributes = field.GetCustomAttributes ();

				CodeExpression formattedExpression = null; 
				foreach (var a in attributes)
				{
					switch (a)
					{
						case HeaderAttribute header:
							inspectorGUICode.Statements.Add (CreateHeaderStatement (field, header));
							break;
						case SpaceAttribute space:
							inspectorGUICode.Statements.Add (CreateSpaceStatement (field, space));
							break;
						case RangeAttribute range:
							formattedExpression = CreateSliderStatement (field, range);
							break;
						case TextAreaAttribute textArea:
							Debug.Log ("TextAreaAttribute not supported yet.");
							break;
						case MultilineAttribute multiline:
							Debug.Log ("MultilineAttribute not supported yet.");
							break;
						case ContextMenuItemAttribute contextMenuItem:
							Debug.Log ("ContextMenuItemAttribute not supported yet.");
							break;
						case GradientUsageAttribute gradientUsage:
							Debug.Log ("GradientUsageAttribute  not supported yet.");
							break;
						case DelayedAttribute delayed:
							Debug.Log ("DelayedAttribute not supported yet.");
							break;
						case MinAttribute min:
							Debug.Log ("MinAttribute not supported yet.");
							break;
					}
				}

				if (formattedExpression != null)
					inspectorGUICode.Statements.Add (formattedExpression);
				else
					inspectorGUICode.Statements.Add (CreatePropertyFieldStatement (field));
			}

			// ApplyModifiedProperties
			inspectorGUICode.Statements.Add (new CodeMethodInvokeExpression (new CodeMethodReferenceExpression (new CodeVariableReferenceExpression ("serializedObject"), "ApplyModifiedProperties")));

			return inspectorGUICode;
		}

		private CodeExpression CreateHeaderStatement (FieldInfo field, HeaderAttribute header)
		{
			var propertyFieldParameters = new CodeExpression[]
			{
				new CodePrimitiveExpression (header.header),
				new CodeVariableReferenceExpression ($"EditorStyles.boldLabel")
			};

			return new CodeMethodInvokeExpression (new CodeTypeReferenceExpression (typeof (EditorGUILayout)), "LabelField", propertyFieldParameters);
		}

		private CodeExpression CreateSpaceStatement (FieldInfo field, SpaceAttribute space)
		{
			var propertyFieldParameters = new CodeExpression[]
			{
				new CodePrimitiveExpression (false),
				new CodePrimitiveExpression (space.height),
			};

			return new CodeMethodInvokeExpression (new CodeTypeReferenceExpression (typeof (EditorGUILayout)), "GetControlRect", propertyFieldParameters);
		}

		private CodeExpression CreatePropertyFieldStatement (FieldInfo field)
		{
			var propertyFieldParameters = new CodeExpression[]
			{
				new CodeMethodInvokeExpression
				(
					new CodeMethodReferenceExpression
					(
						new CodeVariableReferenceExpression
						(
							"serializedObject"
						),
						"FindProperty"
					),
					new CodePrimitiveExpression (field.Name)
				),
				new CodeVariableReferenceExpression ($"{field.Name}Content")
			};

			return new CodeMethodInvokeExpression (new CodeTypeReferenceExpression (typeof (EditorGUILayout)), "PropertyField", propertyFieldParameters);
		}

		private CodeExpression CreateSliderStatement (FieldInfo field, RangeAttribute range)
		{
			var propertyFieldParameters = new CodeExpression[]
			{
				new CodeMethodInvokeExpression
				(
					new CodeMethodReferenceExpression
					(
						new CodeVariableReferenceExpression
						(
							"serializedObject"
						),
						"FindProperty"
					),
					new CodePrimitiveExpression (field.Name)
				),
				new CodePrimitiveExpression (range.min),
				new CodePrimitiveExpression (range.max),
				new CodeVariableReferenceExpression ($"{field.Name}Content")
			};

			return new CodeMethodInvokeExpression (new CodeTypeReferenceExpression (typeof (EditorGUILayout)), "Slider", propertyFieldParameters);
		}

		private static IEnumerable<FieldInfo> GetSerializedFields (MonoScript script)
		{
			var fields = script.GetClass ().GetFields (BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			for (int i = 0; i < fields.Length; i++)
			{
				var field = fields[i];

				var hasSerializeAttribute = false;
				var hasNonSerializedAttribute = false;

				var attributes = field.GetCustomAttributes ();
				foreach (var a in attributes)
				{
					if (a is SerializeField)
						hasSerializeAttribute = true;
					if (a is NonSerializedAttribute)
						hasNonSerializedAttribute = true;
				}

				if ((!hasNonSerializedAttribute && field.IsPublic) || hasSerializeAttribute)
					yield return field;
			}

			yield break;
		}
	}
}