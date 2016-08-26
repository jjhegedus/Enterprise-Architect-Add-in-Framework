﻿
using System;
using System.Collections.Generic;
using System.Linq;
using UML=TSF.UmlToolingFramework.UML;
using UTF_EA=TSF.UmlToolingFramework.Wrappers.EA;
using DB=DatabaseFramework;
using DB_EA = EAAddinFramework.Databases;

namespace EAAddinFramework.Databases.Transformation.DB2
{
	/// <summary>
	/// Description of DB2TableTransformer.
	/// </summary>
	public class DB2TableTransformer:EATableTransformer
	{
		protected List<DB2ColumnTransformer> _columnTransformers = new List<DB2ColumnTransformer>();
		internal List<DB2TableTransformer> dependingTransformers = new List<DB2TableTransformer>();
		internal List<DB2ForeignKeyTransformer> _foreignKeyTransformers = new List<DB2ForeignKeyTransformer>();
		internal UTF_EA.AssociationEnd associationEnd;
		public DB2TableTransformer(Database database):base(database){}
		internal UTF_EA.Class logicalClass
		{
			get{ return logicalClasses.FirstOrDefault() as UTF_EA.Class;}
		}
		
		#region implemented abstract members of EATableTransformer
		protected override void createTable(System.Collections.Generic.List<UML.Classes.Kernel.Class> logicalClasses)
		{
			throw new NotImplementedException();
		}
		protected override void createTable(UTF_EA.Class classElement)
		{
			this._logicalClasses.Add(classElement);
			if (classElement.alias == string.Empty) classElement.alias = "unknown table name";
			this.table = new Table(_database, classElement.alias);
		}

		protected override Column transformLogicalAttribute(UTF_EA.Attribute attribute)
		{
			var columnTransformer = new DB2ColumnTransformer(this._table);
			this._columnTransformers.Add(columnTransformer);
			return (Column) columnTransformer.transformLogicalProperty(attribute);
		}

		public override List<DB.Transformation.ColumnTransformer> columnTransformers {
			get { return _columnTransformers.Cast<DB.Transformation.ColumnTransformer>().ToList();}
			set { _columnTransformers = value.Cast<DB2ColumnTransformer>().ToList();}
		}

		#region implemented abstract members of EATableTransformer


		public override List<DB.Transformation.ForeignKeyTransformer> foreignKeyTransformers {
			get { return _foreignKeyTransformers.Cast<DB.Transformation.ForeignKeyTransformer>().ToList();}
			set { _foreignKeyTransformers = value.Cast<DB2ForeignKeyTransformer>().ToList();}
		}


		#endregion

		public void addRemoteColumnsAndKeys()
		{
			List<DB_EA.Column> involvedColumns = new List<DB_EA.Column>();
			//check attributes
			foreach (var attribute in logicalClass.attributes.Where(x => x.isID).Cast<UTF_EA.Attribute>())
			{
				//get the corresponding transformer
				var columnTransformer = this.columnTransformers.FirstOrDefault( x => attribute.Equals(x.logicalProperty));
				if (columnTransformer != null)
				{
					involvedColumns.Add((DB_EA.Column) columnTransformer.column);
				}
			}
			//add the columns for the primary key of the dependent table
			foreach (var dependingTransformer in this.dependingTransformers) 
			{
				List<Column> FKInvolvedColumns = new List<Column>();
				bool isForeignKey = false;
				if (dependingTransformer.table.primaryKey != null)
				{
					//only add FK's for classes in the same pakage;
					if (dependingTransformer.logicalClass.owningPackage.Equals(this.logicalClass.owningPackage))
					{
						isForeignKey = true;
					}
					foreach (var column in dependingTransformer.table.primaryKey.involvedColumns) 
					{
						//TODO: move transformationlogic to columntransformer
						var newColumn = new Column((DB_EA.Table)table, column.name);
						newColumn.type = column.type;
						newColumn.logicalAttribute = ((DB_EA.Column)column).logicalAttribute;
						if (dependingTransformer.associationEnd != null)
						{
							if (dependingTransformer.associationEnd.upper.integerValue.HasValue 
							    && dependingTransformer.associationEnd.upper.integerValue.Value > 0)
							{
								newColumn.isNotNullable = true;
							}
							if (dependingTransformer.associationEnd.isID)
							{
								involvedColumns.Add(newColumn);
							}
							if (isForeignKey)
							{
								FKInvolvedColumns.Add(newColumn);
							}
							//add columnTransformer
							//get the transformer for the column
							var transformer = dependingTransformer._columnTransformers.First(x => x.column == column);
							this._columnTransformers.Add(new DB2ColumnTransformer(this._table, newColumn, transformer._attribute));
						}
					}
					if (isForeignKey && FKInvolvedColumns.Count > 0)
					{
						//TODO: move transformation logic to foreignkeytransformer
						var newFK = new ForeignKey((Table) table, FKInvolvedColumns);
						newFK.name = "FK_" + this.table.name + "_" + dependingTransformer.table.name + "_1" ; //TODO: sequence number for multple foreign keys
						newFK.foreignTable = dependingTransformer.table;
						newFK.logicalAssociation = (UTF_EA.Association)dependingTransformer.associationEnd.association;
						this.table.constraints.Add(newFK);
						//add the transformer
						this._foreignKeyTransformers.Add(new DB2ForeignKeyTransformer(newFK,(UTF_EA.Association)dependingTransformer.associationEnd.association));
					}
				}
			}
			//create primaryKey
			if (involvedColumns.Count > 0)
			{
				this.table.primaryKey = new DB_EA.PrimaryKey((DB_EA.Table)table, involvedColumns);
				this.table.primaryKey.name = "PK_" + table.name;
			}
			
		}
			

		#endregion

		/// <summary>
		/// gets the Class Elements that are needed for this logical element.
		/// This means the classes to which this element has an association to with
		/// multiplicity of 1..1 or 0..1. We will need these classes because they will create one or more columns in the associated table.
		/// </summary>
		/// <returns>the classes on which this logical element depends for this logical element</returns>
		public List<UTF_EA.AssociationEnd> getDependingAssociationEnds()
		{
			var dependingAssociationEnds = new List<UTF_EA.AssociationEnd>();
			foreach (var logicalClass in this.logicalClasses) 
			{
				foreach (var association in logicalClass.relationships.OfType<UTF_EA.Association>())
				{
					foreach (UTF_EA.AssociationEnd end in association.memberEnds) 
					{
						if (!logicalClass.Equals(end.type) 
					          && end.type is UTF_EA.Class
					          && (end.upper.integerValue.HasValue && end.upper.integerValue == 1))
						{
							dependingAssociationEnds.Add(end);
							break;
						}
					}
				}
			}
			return dependingAssociationEnds;
			
		}
	}
}
