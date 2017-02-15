﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using Hapil;
using NWheels.Extensions;
using NWheels.UI.Core;
using NWheels.UI.Uidl;
using NWheels.Processing;

namespace NWheels.UI.Toolbox
{
    [DataContract(Namespace = UidlDocument.DataContractNamespace)]
    public class EntityMethodForm<TContext, TEntity, TInput, TOutput> :
        WidgetBase<EntityMethodForm<TContext, TEntity, TInput, TOutput>, Empty.Data, EntityMethodForm<TContext, TEntity, TInput, TOutput>.IState>
        where TContext : class
        where TEntity : class
        where TInput : class
    {
        private LambdaExpression _methodCallExpression;

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public EntityMethodForm(string idName, ControlledUidlNode parent)
            : base(idName, parent)
        {
            base.WidgetType = "EntityMethodForm";
            base.TemplateName = "EntityMethodForm";
            base.IsPopupContent = true;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [DataMember]
        public Form<TInput> InputForm { get; set; }
        [DataMember]
        public bool QueryAsEntity { get; set; }
        [DataMember]
        public bool IsNextDialog { get; set; }
        [DataMember]
        public bool SkipInputForm { get; set; }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public UidlNotification ShowModal { get; set; }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public UidlCommand OK { get; set; }
        public UidlCommand Cancel { get; set; }
        public UidlNotification<TContext> ContextSetter { get; set; }
        public UidlNotification OperationStarting { get; set; }
        public UidlNotification<TOutput> OperationCompleted { get; set; }
        public UidlNotification<IPromiseFailureInfo> OperationFailed { get; set; }
        public UidlNotification<TEntity> EntitySetter { get; set; }
        public UidlNotification StateResetter { get; set; }
        public UidlNotification NoEntityWasSelected { get; set; }
        
        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public void AttachTo<TController, TControllerData, TControllerState>(
            PresenterBuilder<TController, TControllerData, TControllerState> controller, 
            UidlCommand command,
            Expression<Action<TEntity, ViewModel<Empty.Data, IState, Empty.Payload>>> onExecute,
            bool skipInputForm = false)
            where TController : ControlledUidlNode
            where TControllerData : class 
            where TControllerState : class
        {
            this.Text = command.Text;
            this.Icon = command.Icon;
            _methodCallExpression = onExecute;
            command.Authorization.OperationName = _methodCallExpression.GetMethodInfo().Name;

            if (!skipInputForm)
            { 
                controller.On(command)
                    .Broadcast(this.StateResetter).TunnelDown()
                    .Then(b => b.Broadcast(this.ShowModal).TunnelDown());
            }
            else
            {
                SkipInputForm = true;
                controller.On(command).InvokeCommand(OK);
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public void AttachTo<TController, TControllerData, TControllerState, TMethodOut>(
            PresenterBuilder<TController, TControllerData, TControllerState> controller,
            UidlCommand command,
            Expression<Func<TEntity, ViewModel<Empty.Data, IState, Empty.Payload>, TMethodOut>> onExecute,
            bool skipInputForm = false)
            where TController : ControlledUidlNode
            where TControllerData : class
            where TControllerState : class
        {
            this.Text = command.Text;
            this.Icon = command.Icon;
            _methodCallExpression = onExecute;
            command.Authorization.OperationName = _methodCallExpression.GetMethodInfo().Name;

            if (!skipInputForm)
            {
                controller.On(command)
                    .Broadcast(this.StateResetter).TunnelDown()
                    .Then(b => b.Broadcast(this.ShowModal).TunnelDown());
            }
            else
            {
                SkipInputForm = true;
                controller.On(command).InvokeCommand(OK);
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public void OnExecute(Expression<Func<TEntity, ViewModel<Empty.Data, IState, Empty.Payload>, TOutput>> callExpression)
        {
            _methodCallExpression = callExpression;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public void OnExecute(Expression<Action<TEntity, ViewModel<Empty.Data, IState, Empty.Payload>>> callExpression)
        {
            _methodCallExpression = callExpression;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected override void DescribePresenter(PresenterBuilder<EntityMethodForm<TContext, TEntity, TInput, TOutput>, Empty.Data, IState> presenter)
        {
            InputForm.UsePascalCase = true;
            InputForm.IsModalPopup = true;

            OK.Kind = CommandKind.Submit;
            OK.Severity = CommandSeverity.Change;
            OK.Icon = "check";
            Cancel.Kind = CommandKind.Reject;
            Cancel.Severity = CommandSeverity.Loose;
            Cancel.Icon = "times";

            presenter.On(ContextSetter).AlterModel(alt => alt.Copy(vm => vm.Input).To(vm => vm.State.Context));
            presenter.On(EntitySetter).AlterModel(alt => alt.Copy(vm => vm.Input).To(vm => vm.State.Entity));
            presenter.On(OK)
                .Broadcast(OperationStarting).BubbleUp()
                .Then(b => b.InvokeEntityMethod<TEntity>(TryGetQueryAsEntityType()).WaitForReplyOrCompletion<TOutput>(_methodCallExpression)
                .Then(
                    onSuccess: 
                        b2 => b2.AlterModel(alt => alt.Copy(vm => vm.Input).To(vm => vm.State.Output))
                        .Then(b3 => b3.Broadcast(OperationCompleted).WithPayload(vm => vm.Input).BubbleUp()
                        .Then(b4 => b4.Broadcast(InputForm.StateResetter).TunnelDown()
                        .Then(b5 => b5.UserAlertFrom<IEntityMethodUserAlerts>().ShowPopup((alerts, vm) => alerts.RequestedOperationSuccessfullyCompleted())))),
                    onFailure:
                        b2 => b2.Broadcast(OperationFailed).WithPayload(vm => vm.Input).BubbleUp()
                        .Then(b3 => b3.Broadcast(InputForm.StateResetter).TunnelDown()
                        .Then(b4 => b4.UserAlertFrom<IEntityMethodUserAlerts>().ShowPopup((alerts, vm) => alerts.RequestedOperationHasFailed(), faultInfo: vm => vm.Input)))
                ));

            presenter.On(NoEntityWasSelected).UserAlertFrom<IEntityMethodUserAlerts>().ShowPopup((alerts, vm) => alerts.NoEntityWasSelected());
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        #region Overrides of WidgetUidlNode

        public override IEnumerable<WidgetUidlNode> GetNestedWidgets()
        {
            return base.GetNestedWidgets().Concat(new WidgetUidlNode[] { InputForm });
        }

        #endregion

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [ViewModelContract]
        public interface IState
        {
            TContext Context { get; set; }
            TEntity Entity { get; set; }
            TInput Input { get; set; }
            TOutput Output { get; set; }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private Type TryGetQueryAsEntityType()
        {
            if ( QueryAsEntity )
            {
                Type collectionElementType;

                if ( typeof(TOutput).IsCollectionType(out collectionElementType) )
                {
                    return collectionElementType;
                }
                else
                {
                    return typeof(TOutput);
                }
            }

            return null;
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------------------------------------

    [DataContract(Namespace = UidlDocument.DataContractNamespace)]
    public class EntityMethodFormWithOutput<TEntity, TInput, TOutput> : EntityMethodForm<Empty.Context, TEntity, TInput, TOutput>
        where TEntity : class
        where TInput : class
        where TOutput : class
    {
        public EntityMethodFormWithOutput(string idName, ControlledUidlNode parent)
            : base(idName, parent)
        {
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public override IEnumerable<WidgetUidlNode> GetNestedWidgets()
        {
            return base.GetNestedWidgets().ConcatOne(OutputForm);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [DataMember]
        public Form<TOutput> OutputForm { get; set; }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected override void DescribePresenter(PresenterBuilder<EntityMethodForm<Empty.Context, TEntity, TInput, TOutput>, Empty.Data, IState> presenter)
        {
            base.DescribePresenter(presenter);
            
            presenter.On(OperationCompleted)
                .Broadcast(OutputForm.ModelSetter).WithPayload(vm => vm.Input).TunnelDown()
                .ThenIf(SkipInputForm, b => b.Broadcast(this.ShowModal).TunnelDown());
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------------------------------------

    [DataContract(Namespace = UidlDocument.DataContractNamespace)]
    public class EntityMethodForm<TEntity, TInput, TOutput> :
        EntityMethodForm<Empty.Context, TEntity, TInput, TOutput>
        where TEntity : class
        where TInput : class
    {
        public EntityMethodForm(string idName, ControlledUidlNode parent)
            : base(idName, parent)
        {
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------------------------------------

    [DataContract(Namespace = UidlDocument.DataContractNamespace)]
    public class EntityMethodForm<TEntity, TInput> :
        EntityMethodForm<Empty.Context, TEntity, TInput, Empty.Output>
        where TEntity : class
        where TInput : class
    {
        public EntityMethodForm(string idName, ControlledUidlNode parent)
            : base(idName, parent)
        {
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------------------------------------

    public interface IEntityMethodUserAlerts : IUserAlertRepository
    {
        [SuccessAlert]
        UidlUserAlert RequestedOperationSuccessfullyCompleted();
        
        [ErrorAlert]
        UidlUserAlert RequestedOperationHasFailed();

        [InfoAlert()]
        UidlUserAlert NoEntityWasSelected();
    }
}