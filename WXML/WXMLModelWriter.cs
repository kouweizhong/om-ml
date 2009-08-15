using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using WXML.Model.Descriptors;
using System.Linq;

namespace WXML.Model
{
    internal class WXMLModelWriter
    {
        private readonly WXMLDocumentSet _wxmlDocumentSet;
        private XmlDocument _ormXmlDocumentMain;
        private readonly WXMLModel _model;

        private readonly XmlNamespaceManager _nsMgr;
        private readonly XmlNameTable _nametable;

        private readonly WXMLModelWriterSettings _settings;

        internal WXMLModelWriter(WXMLModel model, WXMLModelWriterSettings settings)
        {
            _model = model;
            _nametable = new NameTable();
            _nsMgr = new XmlNamespaceManager(_nametable);
            _nsMgr.AddNamespace(WXMLModel.NS_PREFIX, WXMLModel.NS_URI);
            _wxmlDocumentSet = new WXMLDocumentSet();
            _settings = settings;
        }

        internal static WXMLDocumentSet Generate(WXMLModel schema, WXMLModelWriterSettings settings)
        {
            WXMLModelWriter generator = new WXMLModelWriter(schema, settings);

            generator.GenerateXmlDocumentInternal();

            return generator._wxmlDocumentSet;
        }

        public WXMLModel Model
        {
            get
            {
                return _model;
            }
        }

        private void GenerateXmlDocumentInternal()
        {
            CreateXmlDocument();

            FillFileDescriptions();

            FillLinqSettings();

            FillImports();           

        	FillSourceFragments();

            FillTypes();

            FillEntities();

            FillRelations();

            FillExtensions();
        }

        private void FillExtensions()
        {
            if (Model.Extensions.Count > 0)
            {
                var extensionsContainer = CreateElement("extensions");
                _ormXmlDocumentMain.DocumentElement.AppendChild(extensionsContainer);

                foreach (var extension in Model.Extensions)
                {
                    FillExtension(extensionsContainer, extension);
                }
            }
        }

        private void FillExtension(XmlElement extensionsContainer, KeyValuePair<string, XmlDocument> extension)
        {
            var extensionElement = CreateElement("extension");
            extensionsContainer.AppendChild(extensionElement);

            extensionElement.SetAttribute("name", extension.Key);
            extensionElement.InnerXml = extension.Value.InnerXml;
        }

        private void FillLinqSettings()
        {
            if (_model.LinqSettings == null)
                return;

            var linqSettings = CreateElement("Linq");
            _ormXmlDocumentMain.DocumentElement.AppendChild(linqSettings);

            linqSettings.SetAttribute("enable", XmlConvert.ToString(_model.LinqSettings.Enable));

            if (!string.IsNullOrEmpty(_model.LinqSettings.ContextName))
                linqSettings.SetAttribute("contextName", _model.LinqSettings.ContextName);

            if (!string.IsNullOrEmpty(_model.LinqSettings.FileName))
                linqSettings.SetAttribute("filename", _model.LinqSettings.FileName);

            if (!string.IsNullOrEmpty(_model.LinqSettings.BaseContext))
                linqSettings.SetAttribute("baseContext", _model.LinqSettings.BaseContext);

            if (_model.LinqSettings.ContextClassBehaviour.HasValue)
                linqSettings.SetAttribute("contextClassBehaviour",
                                          _model.LinqSettings.ContextClassBehaviour.ToString());
        }

        private void FillImports()
        {
            if(_model.Includes.Count == 0)
                return;
            XmlNode importsNode = CreateElement("Includes");
            _ormXmlDocumentMain.DocumentElement.AppendChild(importsNode);
            foreach (WXMLModel objectsDef in _model.Includes)
            {
                WXMLModelWriterSettings settings = (WXMLModelWriterSettings)_settings.Clone();
                    //settings.DefaultMainFileName = _settings.DefaultIncludeFileName + _ormObjectsDef.Includes.IndexOf(objectsDef);
                    WXMLDocumentSet set;
                    set = Generate(objectsDef, _settings);
                    _wxmlDocumentSet.AddRange(set);
                    foreach (WXMLDocument ormXmlDocument in set)
                    {
                        if ((_settings.IncludeBehaviour & IncludeGenerationBehaviour.Inline) ==
                            IncludeGenerationBehaviour.Inline)
                        {
                            XmlNode importedSchemaNode =
                                _ormXmlDocumentMain.ImportNode(ormXmlDocument.Document.DocumentElement, true);
                            importsNode.AppendChild(importedSchemaNode);
                        }
                        else
                        {
                            XmlElement includeElement =
                                _ormXmlDocumentMain.CreateElement("xi", "include", "http://www.w3.org/2001/XInclude");
                            includeElement.SetAttribute("parse", "xml");

                            string fileName = GetIncludeFileName(_model, objectsDef, settings);

                            includeElement.SetAttribute("href", fileName);
                            importsNode.AppendChild(includeElement);
                        }

                    }
            }
        }

        private void CreateXmlDocument()
        {
            _ormXmlDocumentMain = new XmlDocument(_nametable);
            XmlDeclaration declaration = _ormXmlDocumentMain.CreateXmlDeclaration("1.0", Encoding.UTF8.WebName, null);
            _ormXmlDocumentMain.AppendChild(declaration);
            XmlElement root = CreateElement("WXMLModel");
            _ormXmlDocumentMain.AppendChild(root);
            string filename = GetFilename(_model, _settings);
            WXMLDocument doc = new WXMLDocument(filename, _ormXmlDocumentMain);
            _wxmlDocumentSet.Add(doc);
          
        }

        private string GetFilename(WXMLModel objectsDef, WXMLModelWriterSettings settings)
        {
            return string.IsNullOrEmpty(objectsDef.FileName)
                       ? settings.DefaultMainFileName
                       : objectsDef.FileName;
        }

        private string GetIncludeFileName(WXMLModel objectsDef, WXMLModel incldeObjectsDef, WXMLModelWriterSettings settings)
        {
            if (string.IsNullOrEmpty(incldeObjectsDef.FileName))
            {
                string filename =
                    settings.IncludeFileNamePattern.Replace("%MAIN_FILENAME%", GetFilename(objectsDef, settings)).
                        Replace(
                        "%INCLUDE_NAME%", GetFilename(incldeObjectsDef, settings)) +
                    objectsDef.Includes.IndexOf(incldeObjectsDef);
                return
                    (((settings.IncludeBehaviour & IncludeGenerationBehaviour.PlaceInFolder) ==
                      IncludeGenerationBehaviour.PlaceInFolder)
                         ? settings.IncludeFolderName + System.IO.Path.DirectorySeparatorChar
                         : string.Empty) + filename;
            }
            else
                return incldeObjectsDef.FileName;
        }

        private void FillRelations()
        {
            if (_model.Relations.Count == 0)
                return;
            XmlNode relationsNode = CreateElement("EntityRelations");
            _ormXmlDocumentMain.DocumentElement.AppendChild(relationsNode);
            foreach (RelationDefinitionBase rel in _model.Relations)
            {
                XmlElement relationElement;
                if (rel is RelationDefinition)
                {
					RelationDefinition relation = (RelationDefinition)rel;

                    relationElement = CreateElement("Relation");

                    relationElement.SetAttribute("table", relation.SourceFragment.Identifier);
                    if (relation.Disabled)
                    {
                        relationElement.SetAttribute("disabled", XmlConvert.ToString(relation.Disabled));
                    }

                    XmlElement leftElement = CreateElement("Left");
                    leftElement.SetAttribute("entity", relation.Left.Entity.Identifier);
                    leftElement.SetAttribute("fieldName", relation.Left.FieldName);
                    leftElement.SetAttribute("cascadeDelete", XmlConvert.ToString(relation.Left.CascadeDelete));
					
                    if (!string.IsNullOrEmpty(relation.Left.AccessorName))
						leftElement.SetAttribute("accessorName", relation.Left.AccessorName);

                    if (!string.IsNullOrEmpty(relation.Left.AccessorDescription))
                        leftElement.SetAttribute("accessorDescription", relation.Left.AccessorDescription);

                    if (relation.Left.AccessedEntityType != null)
                        leftElement.SetAttribute("accessedEntityType", relation.Left.AccessedEntityType.Identifier);

                    XmlElement rightElement = CreateElement("Right");
                    rightElement.SetAttribute("entity", relation.Right.Entity.Identifier);
                    rightElement.SetAttribute("fieldName", relation.Right.FieldName);
                    rightElement.SetAttribute("cascadeDelete", XmlConvert.ToString(relation.Right.CascadeDelete));
					
                    if (!string.IsNullOrEmpty(relation.Right.AccessorName))
						rightElement.SetAttribute("accessorName", relation.Right.AccessorName);

                    if (!string.IsNullOrEmpty(relation.Right.AccessorDescription))
                        rightElement.SetAttribute("accessorDescription", relation.Right.AccessorDescription);

                    if (relation.Right.AccessedEntityType != null)
						rightElement.SetAttribute("accessedEntityType", relation.Right.AccessedEntityType.Identifier);

                    if (relation.UnderlyingEntity != null)
                    {
                        relationElement.SetAttribute("underlyingEntity", relation.UnderlyingEntity.Identifier);
                    }

                    relationElement.AppendChild(leftElement);
                    relationElement.AppendChild(rightElement);
                    relationsNode.AppendChild(relationElement);
                }
                else
                {
					SelfRelationDescription relation = (SelfRelationDescription)rel;

                    relationElement = CreateElement("SelfRelation");

                    relationElement.SetAttribute("table", relation.SourceFragment.Identifier);
                    relationElement.SetAttribute("entity", relation.Entity.Identifier);
                    if (relation.Disabled)
                    {
                        relationElement.SetAttribute("disabled", XmlConvert.ToString(relation.Disabled));
                    }

                    XmlElement directElement = CreateElement("Direct");

                    directElement.SetAttribute("fieldName", relation.Direct.FieldName);
                    directElement.SetAttribute("cascadeDelete", XmlConvert.ToString(relation.Direct.CascadeDelete));
					
                    if (!string.IsNullOrEmpty(relation.Direct.AccessorName))
						directElement.SetAttribute("accessorName", relation.Direct.AccessorName);

                    if (!string.IsNullOrEmpty(relation.Direct.AccessorDescription))
                        directElement.SetAttribute("accessorDescription", relation.Direct.AccessorDescription);

                    if (relation.Direct.AccessedEntityType != null)
                        directElement.SetAttribute("accessedEntityType", relation.Direct.AccessedEntityType.Identifier);

                    XmlElement reverseElement = CreateElement("Reverse");
                    reverseElement.SetAttribute("fieldName", relation.Reverse.FieldName);
                    reverseElement.SetAttribute("cascadeDelete", XmlConvert.ToString(relation.Reverse.CascadeDelete));
					
                    if (!string.IsNullOrEmpty(relation.Reverse.AccessorName))
						reverseElement.SetAttribute("accessorName", relation.Reverse.AccessorName);

                    if (!string.IsNullOrEmpty(relation.Reverse.AccessorDescription))
                        reverseElement.SetAttribute("accessorDescription", relation.Reverse.AccessorDescription);

                    if (relation.Reverse.AccessedEntityType != null)
						reverseElement.SetAttribute("accessedEntityType", relation.Reverse.AccessedEntityType.Identifier);

                    if (relation.UnderlyingEntity != null)
                    {
                        relationElement.SetAttribute("underlyingEntity", relation.UnderlyingEntity.Identifier);
                    }
                    relationElement.AppendChild(directElement);
                    relationElement.AppendChild(reverseElement);
                    
                }
                if (rel.Constants.Count > 0)
                {
                    var constantsElement = CreateElement("Constants");
                    relationElement.InsertBefore(constantsElement, relationElement.FirstChild);

                    foreach (var constantDescriptor in rel.Constants)
                    {
                        var constantElement = CreateElement("Constant");
                        constantsElement.AppendChild(constantElement);

                        constantElement.SetAttribute("name", constantDescriptor.Name);
                        constantElement.SetAttribute("value", constantDescriptor.Value);
                    }
                }
                relationsNode.AppendChild(relationElement);
			}
        }

        private void FillEntities()
        {
            XmlNode entitiesNode = CreateElement("Entities");
            _ormXmlDocumentMain.DocumentElement.AppendChild(entitiesNode);

            foreach (EntityDefinition entity in _model.GetEntities())
            {
                XmlElement entityElement = CreateElement("Entity");

                entityElement.SetAttribute("id", entity.Identifier);
                entityElement.SetAttribute("name", entity.Name);
                if (!string.IsNullOrEmpty(entity.Description))
                    entityElement.SetAttribute("description", entity.Description);
                if (entity.Namespace != entity.Model.Namespace)
                    entityElement.SetAttribute("namespace", entity.Namespace);
				if(entity.Behaviour != EntityBehaviuor.Default)
					entityElement.SetAttribute("behaviour", entity.Behaviour.ToString());
				if (entity.UseGenerics)
					entityElement.SetAttribute("useGenerics", XmlConvert.ToString(entity.UseGenerics));
				if (entity.MakeInterface)
					entityElement.SetAttribute("makeInterface", XmlConvert.ToString(entity.MakeInterface));
                if (entity.BaseEntity != null)
                    entityElement.SetAttribute("baseEntity", entity.BaseEntity.Identifier);
				if (entity.Disabled)
					entityElement.SetAttribute("disabled", XmlConvert.ToString(entity.Disabled));
                
                if (!string.IsNullOrEmpty(entity.FamilyName) && entity.FamilyName != entity.Name)
                    entityElement.SetAttribute("familyName", entity.FamilyName);

				if (entity.CacheCheckRequired)
					entityElement.SetAttribute("cacheCheckRequired", XmlConvert.ToString(entity.CacheCheckRequired));
				

                XmlNode tablesNode = CreateElement("SourceFragments");
                foreach (SourceFragmentRefDefinition table in entity.GetSourceFragments())
                {
					XmlElement tableElement = CreateElement("SourceFragment");
                    tableElement.SetAttribute("ref", table.Identifier);
                    if (table.AnchorTable != null)
                    {
                        tableElement.SetAttribute("anchorTableRef", table.AnchorTable.Identifier);
                        tableElement.SetAttribute("type", table.JoinType.ToString());
                        foreach (SourceFragmentRefDefinition.Condition c in table.Conditions)
                        {
                            XmlElement join = CreateElement("join");
                            join.SetAttribute("refColumn", c.LeftColumn);
                            join.SetAttribute("anchorColumn", c.RightColumn);
                            tableElement.AppendChild(join);
                        }
                    }
                    tablesNode.AppendChild(tableElement);
                }
				if(!entity.InheritsBaseTables)
				{
					((XmlElement)tablesNode).SetAttribute("inheritsBase", XmlConvert.ToString(entity.InheritsBaseTables));
				}
				
				entityElement.AppendChild(tablesNode);	

                XmlNode propertiesNode = CreateElement("Properties");
                IEnumerable<PropertyDefinition> properties = entity.SelfProperties.Where(p => p.Group == null);
                FillEntityProperties(properties, propertiesNode);
                entityElement.AppendChild(propertiesNode);

                List<PropertyGroup> groups = new List<PropertyGroup>();
                properties = entity.SelfProperties.Where(p => p.Group != null);

                foreach (var property in properties)
                {
                    if (!groups.Contains(property.Group))
                        groups.Add(property.Group);
                }

                foreach (var propertyGroup in groups)
                {
                    PropertyGroup propertyGroup1 = propertyGroup;
                    var props = properties.Where(p => p.Group == propertyGroup1);

                    XmlElement groupNode = CreateElement("Group");
                    groupNode.SetAttribute("name", propertyGroup.Name);

                    if (!propertyGroup.Hide)
                        groupNode.SetAttribute("hide", XmlConvert.ToString(propertyGroup.Hide));

                    FillEntityProperties(props, groupNode);
                }
                if (entity.EntityRelations.Count > 0)
                {
                    XmlNode relationsNode = CreateElement("Relations");

                    foreach (var entityRelation in entity.EntityRelations)
                    {
                        var relationNode = CreateElement("Relation");

                        relationNode.SetAttribute("entity", entityRelation.Entity.Identifier);

                        if (!string.IsNullOrEmpty(entityRelation.PropertyAlias))
                            relationNode.SetAttribute("propertyAlias", entityRelation.PropertyAlias);

                        if (!string.IsNullOrEmpty(entityRelation.Name))
                            relationNode.SetAttribute("name", entityRelation.Name);

                        if (!string.IsNullOrEmpty(entityRelation.AccessorName))
                            relationNode.SetAttribute("accessorName", entityRelation.AccessorName);

                        if (!entityRelation.Disabled)
                            relationNode.SetAttribute("disabled", XmlConvert.ToString(entityRelation.Disabled));

                        if (!string.IsNullOrEmpty(entityRelation.AccessorDescription))
                            relationNode.SetAttribute("accessorDescription", entityRelation.AccessorDescription);

                        relationsNode.AppendChild(relationNode);
                    }

                    entityElement.AppendChild(relationsNode);
                }

                if (entity.Extensions.Count > 0)
                {
                    foreach (var extension in entity.Extensions)
                    {
                        FillExtension(entityElement, extension);
                    }
                }

                entitiesNode.AppendChild(entityElement);
            }
        }

        private void FillEntityProperties(IEnumerable<PropertyDefinition> properties, XmlNode propertiesNode)
        {
            foreach (PropertyDefinition property in properties)
            {
                XmlElement propertyElement = CreateElement("Property");
                propertyElement.SetAttribute("propertyName", property.Name);
                if(property.Attributes != Field2DbRelations.None /*null && property.Attributes.Length > 0*/)
                {
                    propertyElement.SetAttribute("attributes", Enum.GetName(typeof(Field2DbRelations), property.Attributes) /*string.Join(" ", property.Attributes)*/);
                }
                if (property.SourceFragment != null)
                    propertyElement.SetAttribute("table", property.SourceFragment.Identifier);
                propertyElement.SetAttribute("fieldName", property.FieldName);
                if (property.PropertyType != null)
                    propertyElement.SetAttribute("typeRef", property.PropertyType.Identifier);
                if (!string.IsNullOrEmpty(property.Description))
                    propertyElement.SetAttribute("description", property.Description);
                if (property.FieldAccessLevel != AccessLevel.Private)
                    propertyElement.SetAttribute("classfieldAccessLevel", property.FieldAccessLevel.ToString());
                if (property.PropertyAccessLevel != AccessLevel.Public)
                    propertyElement.SetAttribute("propertyAccessLevel", property.PropertyAccessLevel.ToString());
                if (property.PropertyAlias != property.Name)
                    propertyElement.SetAttribute("propertyAlias", property.PropertyAlias);
                if (property.Disabled)
                    propertyElement.SetAttribute("disabled", XmlConvert.ToString(true));
                if (property.Obsolete != ObsoleteType.None)
                    propertyElement.SetAttribute("obsolete", property.Obsolete.ToString());
                if(!string.IsNullOrEmpty(property.ObsoleteDescripton))
                    propertyElement.SetAttribute("obsoleteDescription", property.ObsoleteDescripton);
                if (property.EnablePropertyChanged)
                    propertyElement.SetAttribute("enablePropertyChanged", XmlConvert.ToString(property.EnablePropertyChanged));
                if (!string.IsNullOrEmpty(property.DbTypeName))
                    propertyElement.SetAttribute("dbTypeName", property.DbTypeName);
                if (property.DbTypeSize.HasValue)
                    propertyElement.SetAttribute("dbTypeSize", XmlConvert.ToString(property.DbTypeSize.Value));
                if (property.DbTypeNullable.HasValue)
                    propertyElement.SetAttribute("dbTypeNullable", XmlConvert.ToString(property.DbTypeNullable.Value));
				if (!string.IsNullOrEmpty(property.DefferedLoadGroup))
					propertyElement.SetAttribute("defferedLoadGroup", property.DefferedLoadGroup);
                if (!string.IsNullOrEmpty(property.FieldAlias))
                    propertyElement.SetAttribute("fieldAlias", property.FieldAlias);
                propertiesNode.AppendChild(propertyElement);
            }
        }

        private void FillTypes()
        {
            XmlNode typesNode = CreateElement("Types");
            _ormXmlDocumentMain.DocumentElement.AppendChild(typesNode);
            foreach (TypeDefinition type in _model.GetTypes())
            {
                XmlElement typeElement = CreateElement("Type");

                typeElement.SetAttribute("id", type.Identifier);

                XmlElement typeSubElement;
                if(type.IsClrType)
                {
                    typeSubElement = CreateElement("ClrType");
                    typeSubElement.SetAttribute("name", type.ClrTypeName);
                }
                else if(type.IsUserType)
                {
                    typeSubElement = CreateElement("UserType");
                    typeSubElement.SetAttribute("name", type.GetTypeName(null));
                    if(type.UserTypeHint.HasValue && type.UserTypeHint != UserTypeHintFlags.None)
                    {
                        typeSubElement.SetAttribute("hint", type.UserTypeHint.ToString().Replace(",", string.Empty));
                    }
                }
                else
                {
                    typeSubElement = CreateElement("Entity");
                    typeSubElement.SetAttribute("ref", type.Entity.Identifier);
                }
                typeElement.AppendChild(typeSubElement);
                typesNode.AppendChild(typeElement);
            }
        }

		private void FillSourceFragments()
		{
			XmlElement tablesNode = CreateElement("SourceFragments");
			_ormXmlDocumentMain.DocumentElement.AppendChild(tablesNode);
			foreach (SourceFragmentDefinition table in _model.GetSourceFragments())
			{
				XmlElement tableElement = CreateElement("SourceFragment");
				tableElement.SetAttribute("id", table.Identifier);
				tableElement.SetAttribute("name", table.Name);
				if(!string.IsNullOrEmpty(table.Selector))
					tableElement.SetAttribute("selector", table.Selector);
				tablesNode.AppendChild(tableElement);
			}
		}

        private XmlElement CreateElement(string name)
        {
            return _ormXmlDocumentMain.CreateElement(WXMLModel.NS_PREFIX, name, WXMLModel.NS_URI);
        }

        private void FillFileDescriptions()
        {
            if (!string.IsNullOrEmpty(_model.Namespace))
                _ormXmlDocumentMain.DocumentElement.SetAttribute("defaultNamespace", _model.Namespace);

            if (!string.IsNullOrEmpty(_model.SchemaVersion))
                _ormXmlDocumentMain.DocumentElement.SetAttribute("schemaVersion", _model.SchemaVersion);
			
            if (!string.IsNullOrEmpty(_model.EntityBaseTypeName))
				_ormXmlDocumentMain.DocumentElement.SetAttribute("entityBaseType", _model.EntityBaseTypeName);
			
            if (_model.EnableCommonPropertyChangedFire)
				_ormXmlDocumentMain.DocumentElement.SetAttribute("enableCommonPropertyChangedFire",
				                                                 XmlConvert.ToString(_model.EnableCommonPropertyChangedFire));
            if (!_model.GenerateEntityName)
                _ormXmlDocumentMain.DocumentElement.SetAttribute("generateEntityName",
                                                                 XmlConvert.ToString(_model.GenerateEntityName));

            StringBuilder commentBuilder = new StringBuilder();
            foreach (string comment in _model.SystemComments)
            {
                commentBuilder.AppendLine(comment);
            }

            if(_model.UserComments.Count > 0)
            {
                commentBuilder.AppendLine();
                foreach (string comment in _model.UserComments)
                {
                    commentBuilder.AppendLine(comment);
                }
            }

            XmlComment commentsElement =
                _ormXmlDocumentMain.CreateComment(commentBuilder.ToString());
            _ormXmlDocumentMain.InsertBefore(commentsElement, _ormXmlDocumentMain.DocumentElement);
        }
    }

    public class WXMLDocument
    {
        private XmlDocument m_document;
        private string m_fileName;

        public WXMLDocument(string filename, XmlDocument document)
        {
            if (string.IsNullOrEmpty(filename))
                throw new ArgumentNullException("filename");
            if (document == null)
                throw new ArgumentNullException("document");
            m_document = document;
            m_fileName = filename;
        }

        public XmlDocument Document
        {
            get { return m_document; }
            set { m_document = value; }
        }

        public string FileName
        {
            get { return m_fileName; }
            set { m_fileName = value; }
        }
    }

    public class WXMLDocumentSet : List<WXMLDocument>
    {
    }
}
