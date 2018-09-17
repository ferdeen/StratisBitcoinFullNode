﻿using Moq;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.Executor.Reflection;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class StateTransitionSpecification
    {
        private readonly Mock<IContractState> trackedState;
        private readonly Mock<IContractStateRoot> contractStateRoot;
        private readonly Mock<IAddressGenerator> addressGenerator;
        private readonly Mock<ISmartContractVirtualMachine> vm;
        private readonly Mock<IContractState> trackedState2;

        public StateTransitionSpecification()
        {
            this.trackedState = new Mock<IContractState>();
            this.contractStateRoot = new Mock<IContractStateRoot>();
            this.contractStateRoot.Setup(c => c.StartTracking())
                .Returns(this.trackedState.Object);
            this.trackedState2 = new Mock<IContractState>();
            this.trackedState.Setup(c => c.StartTracking())
                .Returns(this.trackedState2.Object);
            this.addressGenerator = new Mock<IAddressGenerator>();
            this.vm = new Mock<ISmartContractVirtualMachine>();
        }

        [Fact]
        public void ExternalCreate_Success()
        {
            var newContractAddress = uint160.One;
            var vmExecutionResult = VmExecutionResult.Success(true, "Test");

            var externalCreateMessage = new ExternalCreateMessage(
                uint160.Zero,
                10,
                (Gas)(GasPriceList.BaseCost + 100000),
                new byte[0],
                null
            );

            this.vm.Setup(v => v.Create(this.contractStateRoot.Object, It.IsAny<ISmartContractState>(), externalCreateMessage.Code, externalCreateMessage.Parameters, null))
                .Returns(vmExecutionResult);

            var state = new Mock<IState>();
            state.SetupGet(s => s.ContractState).Returns(this.contractStateRoot.Object);
            state.Setup(s => s.GenerateAddress(It.IsAny<IAddressGenerator>())).Returns(newContractAddress);

            var stateProcessor = new StateProcessor(this.vm.Object, this.addressGenerator.Object);

            StateTransitionResult result = stateProcessor.Apply(state.Object, externalCreateMessage);

            state.Verify(s => s.GenerateAddress(this.addressGenerator.Object), Times.Once);

            this.contractStateRoot.Verify(s => s.CreateAccount(newContractAddress), Times.Once);

            state.Verify(s => s.CreateSmartContractState(state.Object, It.IsAny<GasMeter>(), newContractAddress, externalCreateMessage, this.contractStateRoot.Object));

            this.vm.Verify(v => v.Create(this.contractStateRoot.Object, It.IsAny<ISmartContractState>(), externalCreateMessage.Code, externalCreateMessage.Parameters, null), Times.Once);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Success);
            Assert.Equal(newContractAddress, result.Success.ContractAddress);
            Assert.Equal(vmExecutionResult.Result, result.Success.ExecutionResult);
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
        }

        [Fact]
        public void ExternalCreate_Vm_Error()
        {
            var newContractAddress = uint160.One;
            var vmExecutionResult = VmExecutionResult.Error(new ContractErrorMessage("Error"));

            var externalCreateMessage = new ExternalCreateMessage(
                uint160.Zero,
                10,
                (Gas)(GasPriceList.BaseCost + 100000),
                new byte[0],
                null
            );

            this.vm.Setup(v => v.Create(this.contractStateRoot.Object, It.IsAny<ISmartContractState>(), externalCreateMessage.Code, externalCreateMessage.Parameters, null))
                .Returns(vmExecutionResult);

            var state = new Mock<IState>();
            state.SetupGet(s => s.ContractState).Returns(this.contractStateRoot.Object);
            state.Setup(s => s.GenerateAddress(It.IsAny<IAddressGenerator>())).Returns(newContractAddress);

            var stateProcessor = new StateProcessor(this.vm.Object, this.addressGenerator.Object);

            StateTransitionResult result = stateProcessor.Apply(state.Object, externalCreateMessage);

            state.Verify(s => s.GenerateAddress(this.addressGenerator.Object), Times.Once);

            this.contractStateRoot.Verify(ts => ts.CreateAccount(newContractAddress), Times.Once);

            this.vm.Verify(v => v.Create(this.contractStateRoot.Object, It.IsAny<ISmartContractState>(), externalCreateMessage.Code, externalCreateMessage.Parameters, null), Times.Once);

            Assert.False(result.IsSuccess);
            Assert.True(result.IsFailure);
            Assert.NotNull(result.Error);
            Assert.Equal(vmExecutionResult.ErrorMessage, result.Error.VmError);
            Assert.Equal(StateTransitionErrorKind.VmError, result.Error.Kind);
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
        }

        [Fact]
        public void ExternalCall_Success()
        {
            var gasLimit = (Gas)(GasPriceList.BaseCost + 100000);
            var vmExecutionResult = VmExecutionResult.Success(true, "Test");

            // Code must have a length to pass precondition checks.
            var code = new byte[1];
            var typeName = "Test";

            var externalCallMessage = new ExternalCallMessage(
                uint160.Zero,
                uint160.Zero,
                0,
                gasLimit,
                new MethodCall("Test", null)
            );

            this.contractStateRoot
                .Setup(sr => sr.GetCode(externalCallMessage.To))
                .Returns(code);

            this.contractStateRoot
                .Setup(sr => sr.GetContractType(externalCallMessage.To))
                .Returns(typeName);

            this.vm.Setup(v =>
                    v.ExecuteMethod(It.IsAny<ISmartContractState>(), externalCallMessage.Method, code, typeName))
                .Returns(vmExecutionResult);

            var state = new Mock<IState>();
            state.SetupGet(s => s.ContractState).Returns(this.contractStateRoot.Object);

            var stateProcessor = new StateProcessor(this.vm.Object, this.addressGenerator.Object);

            StateTransitionResult result = stateProcessor.Apply(state.Object, externalCallMessage);

            this.contractStateRoot.Verify(sr => sr.GetCode(externalCallMessage.To), Times.Once);

            this.contractStateRoot.Verify(sr => sr.GetContractType(externalCallMessage.To), Times.Once);

            state.Verify(s => s.CreateSmartContractState(state.Object, It.IsAny<GasMeter>(), externalCallMessage.To, externalCallMessage, this.contractStateRoot.Object));

            this.vm.Verify(
                v => v.ExecuteMethod(It.IsAny<ISmartContractState>(), externalCallMessage.Method, code, typeName),
                Times.Once);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Success);
            Assert.Equal(externalCallMessage.To, result.Success.ContractAddress);
            Assert.Equal(vmExecutionResult.Result, result.Success.ExecutionResult);
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
        }

        [Fact]
        public void ExternalCall_Vm_Error()
        {
            var gasLimit = (Gas)(GasPriceList.BaseCost + 100000);
            var vmExecutionResult = VmExecutionResult.Error(new ContractErrorMessage("Error"));

            // Code must have a length to pass precondition checks.
            var code = new byte[1];
            var typeName = "Test";

            var externalCallMessage = new ExternalCallMessage(
                uint160.Zero,
                uint160.Zero,
                0,
                gasLimit,
                new MethodCall("Test")
            );

            this.contractStateRoot
                .Setup(sr => sr.GetCode(externalCallMessage.To))
                .Returns(code);

            this.contractStateRoot
                .Setup(sr => sr.GetContractType(externalCallMessage.To))
                .Returns(typeName);

            this.vm.Setup(v =>
                    v.ExecuteMethod(It.IsAny<ISmartContractState>(), externalCallMessage.Method, code, typeName))
                .Returns(vmExecutionResult);

            var state = new Mock<IState>();
            state.SetupGet(s => s.ContractState).Returns(this.contractStateRoot.Object);
            
            var stateProcessor = new StateProcessor(this.vm.Object, this.addressGenerator.Object);

            StateTransitionResult result = stateProcessor.Apply(state.Object, externalCallMessage);

            this.contractStateRoot.Verify(sr => sr.GetCode(externalCallMessage.To), Times.Once);

            this.contractStateRoot.Verify(sr => sr.GetContractType(externalCallMessage.To), Times.Once);

            state.Verify(s => s.CreateSmartContractState(state.Object, It.IsAny<GasMeter>(), externalCallMessage.To, externalCallMessage, this.contractStateRoot.Object));

            this.vm.Verify(
                v => v.ExecuteMethod(It.IsAny<ISmartContractState>(), externalCallMessage.Method, code, typeName),
                Times.Once);

            Assert.True(result.IsFailure);
            Assert.NotNull(result.Error);
            Assert.Equal(result.Error.VmError, vmExecutionResult.ErrorMessage);
            Assert.Equal(StateTransitionErrorKind.VmError, result.Error.Kind);
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
        }

        [Fact]
        public void InternalCreate_Success()
        {
            // The difference between an internal and an external create:
            // - Internal create performs a balance check before execution
            // - Internal create appends a new internal transfer if successful
            var newContractAddress = uint160.One;
            var vmExecutionResult = VmExecutionResult.Success(true, "Test");
            var code = new byte[1];
            var typeName = "Test";

            var internalCreateMessage = new InternalCreateMessage(
                uint160.Zero,
                10,
                (Gas)(GasPriceList.BaseCost + 100000),
                new object[] {},
                typeName
            );

            this.contractStateRoot
                .Setup(sr => sr.GetCode(internalCreateMessage.From))
                .Returns(code);

            this.vm.Setup(v => v.Create(this.contractStateRoot.Object, It.IsAny<ISmartContractState>(), code, internalCreateMessage.Parameters, internalCreateMessage.Type))
                .Returns(vmExecutionResult);

            var state = new Mock<IState>();

            // Return the sent amount + 1
            state.Setup(s => s.GetBalance(internalCreateMessage.From)).Returns(internalCreateMessage.Amount + 1);
            state.SetupGet(s => s.ContractState).Returns(this.contractStateRoot.Object);
            state.Setup(s => s.GenerateAddress(It.IsAny<IAddressGenerator>())).Returns(newContractAddress);

            var stateProcessor = new StateProcessor(this.vm.Object, this.addressGenerator.Object);

            StateTransitionResult result = stateProcessor.Apply(state.Object, internalCreateMessage);

            // Verify we check the balance of the sender first
            state.Verify(s => s.GetBalance(internalCreateMessage.From));

            // Because this is an internal create we get the code from the sender's code cache
            this.contractStateRoot.Verify(s => s.GetCode(internalCreateMessage.From), Times.Once);

            state.Verify(s => s.GenerateAddress(this.addressGenerator.Object), Times.Once);

            // Verify the account was created
            this.contractStateRoot.Verify(s => s.CreateAccount(newContractAddress), Times.Once);

            // Verify we set up the smart contract state
            state.Verify(s => s.CreateSmartContractState(state.Object, It.IsAny<GasMeter>(), newContractAddress, internalCreateMessage, this.contractStateRoot.Object));

            // Verify the VM was invoked
            this.vm.Verify(v => v.Create(this.contractStateRoot.Object, It.IsAny<ISmartContractState>(), code, internalCreateMessage.Parameters, internalCreateMessage.Type), Times.Once);

            // Verify the value was added to the internal transfer list
            state.Verify(s => s.AddInternalTransfer(It.Is<TransferInfo>(t => t.From == internalCreateMessage.From 
                                                                             && t.To == newContractAddress
                                                                             && t.Value == internalCreateMessage.Amount)));

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Success);
            Assert.Equal(newContractAddress, result.Success.ContractAddress);
            Assert.Equal(vmExecutionResult.Result, result.Success.ExecutionResult);
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
        }

        [Fact]
        public void InternalCall_Success()
        {
            // The difference between an internal and an external call:
            // - Internal call performs a balance check before execution
            // - Internal call appends a new internal transfer if successful
            var vmExecutionResult = VmExecutionResult.Success(true, "Test");
            var code = new byte[1];
            var typeName = "Test";

            var internalCallMessage = new InternalCallMessage(
                uint160.One,
                uint160.Zero,
                10,
                (Gas)(GasPriceList.BaseCost + 100000),
                new MethodCall("Test", new object[] {})
            );

            this.contractStateRoot
                .Setup(sr => sr.GetCode(internalCallMessage.To))
                .Returns(code);

            this.contractStateRoot
                .Setup(sr => sr.GetContractType(internalCallMessage.To))
                .Returns(typeName);

            this.vm.Setup(v => v.ExecuteMethod(It.IsAny<ISmartContractState>(), internalCallMessage.Method, code, typeName))
                .Returns(vmExecutionResult);

            var state = new Mock<IState>();

            // Return the sent amount + 1
            state.Setup(s => s.GetBalance(internalCallMessage.From)).Returns(internalCallMessage.Amount + 1);

            state.SetupGet(s => s.ContractState).Returns(this.contractStateRoot.Object);
            
            var stateProcessor = new StateProcessor(this.vm.Object, this.addressGenerator.Object);

            StateTransitionResult result = stateProcessor.Apply(state.Object, internalCallMessage);

            // Verify we check the balance of the sender first
            state.Verify(s => s.GetBalance(internalCallMessage.From));

            // Verify we get the code from the destination address' code cache
            this.contractStateRoot.Verify(s => s.GetCode(internalCallMessage.To), Times.Once);

            // Verify we set up the smart contract state
            state.Verify(s => s.CreateSmartContractState(state.Object, It.IsAny<GasMeter>(), internalCallMessage.To, internalCallMessage, this.contractStateRoot.Object));

            // Verify the VM was invoked
            this.vm.Verify(v => v.ExecuteMethod(It.IsAny<ISmartContractState>(), internalCallMessage.Method, code, typeName), Times.Once);

            // Verify the value was added to the internal transfer list
            state.Verify(s => s.AddInternalTransfer(It.Is<TransferInfo>(t => t.From == internalCallMessage.From
                                                                             && t.To == internalCallMessage.To
                                                                             && t.Value == internalCallMessage.Amount)));

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Success);
            Assert.Equal(internalCallMessage.To, result.Success.ContractAddress);
            Assert.Equal(vmExecutionResult.Result, result.Success.ExecutionResult);
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
        }

        [Fact]
        public void Contract_Transfer_To_Other_Contract_Success()
        {
            // There is code at the destination address, which causes an internal call to the receive method
            var vmExecutionResult = VmExecutionResult.Success(true, "Test");
            var code = new byte[1];
            var typeName = "Test";

            var contractTransferMessage = new ContractTransferMessage(
                uint160.One,
                uint160.Zero,
                10,
                (Gas)(GasPriceList.BaseCost + 100000)
            );

            // Code must be returned for this test to ensure we apply the call.
            this.contractStateRoot
                .Setup(sr => sr.GetCode(contractTransferMessage.To))
                .Returns(code);

            this.contractStateRoot
                .Setup(sr => sr.GetContractType(contractTransferMessage.To))
                .Returns(typeName);

            this.vm.Setup(v => v.ExecuteMethod(It.IsAny<ISmartContractState>(), contractTransferMessage.Method, code, typeName))
                .Returns(vmExecutionResult);

            var state = new Mock<IState>();

            // Return the sent amount + 1
            state.Setup(s => s.GetBalance(contractTransferMessage.From)).Returns(contractTransferMessage.Amount + 1);

            state.SetupGet(s => s.ContractState).Returns(this.contractStateRoot.Object);

            var stateProcessor = new StateProcessor(this.vm.Object, this.addressGenerator.Object);

            StateTransitionResult result = stateProcessor.Apply(state.Object, contractTransferMessage);

            // Verify we check the balance of the sender first
            state.Verify(s => s.GetBalance(contractTransferMessage.From));

            // Verify we get the code from the destination address' code cache
            this.contractStateRoot.Verify(s => s.GetCode(contractTransferMessage.To), Times.Once);

            // Verify we set up the smart contract state
            state.Verify(s => s.CreateSmartContractState(state.Object, It.IsAny<GasMeter>(), contractTransferMessage.To, contractTransferMessage, this.contractStateRoot.Object));

            // Verify the VM was invoked
            this.vm.Verify(v => v.ExecuteMethod(It.IsAny<ISmartContractState>(), contractTransferMessage.Method, code, typeName), Times.Once);

            // Verify the value was added to the internal transfer list
            state.Verify(s => s.AddInternalTransfer(It.Is<TransferInfo>(t => t.From == contractTransferMessage.From
                                                                             && t.To == contractTransferMessage.To
                                                                             && t.Value == contractTransferMessage.Amount)));

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Success);
            Assert.Equal(contractTransferMessage.To, result.Success.ContractAddress);
            Assert.Equal(vmExecutionResult.Result, result.Success.ExecutionResult);
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
        }

        [Fact]
        public void Contract_Transfer_To_Other_P2PKH_Success()
        {
            // There is no code at the destination address, which causes a regular P2PKH transaction to be created
            var emptyCode = new byte[0];

            var contractTransferMessage = new ContractTransferMessage(
                uint160.One,
                uint160.Zero,
                10,
                (Gas)(GasPriceList.BaseCost + 100000)
            );

            // No code should be returned
            this.contractStateRoot
                .Setup(sr => sr.GetCode(contractTransferMessage.To))
                .Returns(emptyCode);

            var state = new Mock<IState>();

            // Return the sent amount + 1
            state.Setup(s => s.GetBalance(contractTransferMessage.From)).Returns(contractTransferMessage.Amount + 1);

            state.SetupGet(s => s.ContractState).Returns(this.contractStateRoot.Object);

            var stateProcessor = new StateProcessor(this.vm.Object, this.addressGenerator.Object);

            StateTransitionResult result = stateProcessor.Apply(state.Object, contractTransferMessage);

            // Verify we check the balance of the sender first
            state.Verify(s => s.GetBalance(contractTransferMessage.From));

            // Verify we get the code from the destination address' code cache
            this.contractStateRoot.Verify(s => s.GetCode(contractTransferMessage.To), Times.Once);

            // Verify the VM was NOT invoked
            this.vm.Verify(v => v.ExecuteMethod(It.IsAny<ISmartContractState>(), It.IsAny<MethodCall>(), It.IsAny<byte[]>(), It.IsAny<string>()), Times.Never);

            // Verify the value was added to the internal transfer list
            state.Verify(s => s.AddInternalTransfer(It.Is<TransferInfo>(t => t.From == contractTransferMessage.From
                                                                             && t.To == contractTransferMessage.To
                                                                             && t.Value == contractTransferMessage.Amount)));

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Success);
            Assert.Equal(contractTransferMessage.To, result.Success.ContractAddress);
            Assert.Null(result.Success.ExecutionResult);

            // No gas is consumed
            Assert.Equal((Gas) 0, result.GasConsumed);
        }
    }
}