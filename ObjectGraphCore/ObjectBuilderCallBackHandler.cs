﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BorsukSoftware.ObjectGraph
{
	/// <summary>
	/// Class containing functionality to allow for object builders to provide post-build call back functionality
	/// </summary>
	/// <remarks>This functionality (<see cref="PostDependencyBuildCallBack(ObjectBuildingInfo{TAddress})"/> will subsequently register a
	/// job on the appropriate <see cref="Tasks.TaskRunner"/> for the relevant context</remarks>
	/// <typeparam name="TAddress">The type of the address</typeparam>
	public class ObjectBuilderCallBackHandler<TAddress> : Tasks.IJob
	{
		#region Member variables

		private volatile int _outstandingDependencies;

		#endregion

		#region Data Model

		/// <summary>
		/// Gets the <see cref="ObjectBuildingInfo{TAddress}"/> object that this call back is for
		/// </summary>
		public ObjectBuildingInfo<TAddress> ObjectBuilderInfo { get; private set; }

		public ICollection<Tuple<ObjectBuilders.IDependency<TAddress>, ObjectBuildingInfo<TAddress>>> Dependencies { get; private set; }

		#endregion

		#region Construction Logic

		public ObjectBuilderCallBackHandler( ObjectBuildingInfo<TAddress> objectBuilderInfo,
			IEnumerable<Tuple<ObjectBuilders.IDependency<TAddress>, ObjectBuildingInfo<TAddress>>> dependencies )
		{
			if( dependencies == null )
				dependencies = new Tuple<ObjectBuilders.IDependency<TAddress>, ObjectBuildingInfo<TAddress>> [ 0 ];

			this.Dependencies = new List<Tuple<ObjectBuilders.IDependency<TAddress>, ObjectBuildingInfo<TAddress>>>( dependencies );
			this.ObjectBuilderInfo = objectBuilderInfo;

			this._outstandingDependencies = this.Dependencies.Count;
			if( _outstandingDependencies == 0 )
				this.RunBuilder();
		}

		#endregion

		#region Class functionality

		/// <summary>
		/// Functionality which provides the post dependency building call back
		/// </summary>
		/// <remarks>This functionality uses <see cref="System.Threading.Interlocked.Decrement(ref int)"/> in order to avoid
		/// multiple locks / wait handles etc.</remarks>
		/// <param name="dependency">The dependency which has just completed</param>
		public void PostDependencyBuildCallBack( ObjectBuildingInfo<TAddress> dependency )
		{
			var outstanding = System.Threading.Interlocked.Decrement( ref _outstandingDependencies );
			if( outstanding != 0 )
				return;

			this.RunBuilder();
		}

		#endregion

		#region Class functionality (private)

		private void RunBuilder()
		{
			this.ObjectBuilderInfo.ObjectContext.TaskRunner.RegisterJob( this );
		}

		#region IJob Members

		public void Cancel()
		{
			this.ObjectBuilderInfo.SetObjectFailed( new InvalidOperationException( "Operation Cancelled" ) );
		}

		public void Run()
		{
			try
			{
				var dependencySet = new ObjectBuilders.BuiltDependencies<TAddress>();
				if( this.Dependencies.Any( tuple => tuple.Item2.ObjectBuildingState != ObjectBuildingStates.ObjectBuilt ) )
				{
					this.ObjectBuilderInfo.SetObjectFailed( new InvalidOperationException( "Dependency Failed" ) );
					return;
				}

				foreach( var entry in this.Dependencies )
					dependencySet.AddDependency( entry.Item1.Name,
						entry.Item1.Address,
						entry.Item2.BuiltObject );

				var context = new ObjectBuilders.ObjectBuilderBuildObjectContext<TAddress>( this.ObjectBuilderInfo.ObjectContext );

				var builtObject = this.ObjectBuilderInfo.ObjectBuilder.BuildObject( context, this.ObjectBuilderInfo.Address, dependencySet );

				this.ObjectBuilderInfo.SetObjectBuilt( builtObject );
			}
			catch( Exception ex )
			{
				this.ObjectBuilderInfo.SetObjectFailed( ex );
			}
		}

		#endregion

		#endregion
	}
}
