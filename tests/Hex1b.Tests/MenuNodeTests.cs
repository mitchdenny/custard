using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Tests;

public class MenuNodeTests
{
    [Fact]
    public void MenuPopupNode_ConfiguresUpDownBindings()
    {
        // Arrange
        var ownerNode = new MenuNode
        {
            Label = "File",
            Children = [],
            ChildAccelerators = []
        };
        var popupNode = new MenuPopupNode
        {
            OwnerNode = ownerNode
        };
        
        // Act
        var builder = new InputBindingsBuilder();
        popupNode.ConfigureDefaultBindings(builder);
        var bindings = builder.Build();
        
        // Assert
        Assert.NotEmpty(bindings);
        
        // Check that UpArrow and DownArrow bindings exist
        var upArrowBinding = bindings.FirstOrDefault(b => 
            b.Steps.Count == 1 && 
            b.Steps[0].Key == Hex1bKey.UpArrow && 
            b.Steps[0].Modifiers == Hex1bModifiers.None);
        
        var downArrowBinding = bindings.FirstOrDefault(b => 
            b.Steps.Count == 1 && 
            b.Steps[0].Key == Hex1bKey.DownArrow && 
            b.Steps[0].Modifiers == Hex1bModifiers.None);
            
        Assert.NotNull(upArrowBinding);
        Assert.NotNull(downArrowBinding);
    }
    
    [Fact]
    public async Task InputRouter_FindsMenuPopupBindings_WhenChildIsFocused()
    {
        // Arrange - simulate the tree structure when a menu is open
        // ZStack → BackdropNode → AnchoredNode → MenuPopupNode → MenuItemNode
        
        var ownerNode = new MenuNode
        {
            Label = "File",
            Children = [],
            ChildAccelerators = []
        };
        
        var popupNode = new MenuPopupNode
        {
            OwnerNode = ownerNode
        };
        
        var menuItem = new MenuItemNode
        {
            Label = "Open",
            IsFocused = true
        };
        menuItem.Parent = popupNode;
        
        popupNode.ChildNodes = [menuItem];
        
        // Create a simple tree with popupNode → menuItem
        var zstack = new ZStackNode();
        zstack.Children = [popupNode];
        popupNode.Parent = zstack;
        
        // Act - route a down arrow key
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None);
        var focusRing = new FocusRing();
        focusRing.Rebuild(zstack);
        var state = new InputRouterState();
        
        var result = await InputRouter.RouteInputAsync(zstack, keyEvent, focusRing, state, cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert - the binding should be found and handled
        Assert.Equal(InputResult.Handled, result);
    }
    
    [Fact]
    public async Task MenuItemNode_DownArrow_MovesFocusToNextItem()
    {
        // Arrange - set up a popup with two menu items
        var ownerNode = new MenuNode
        {
            Label = "File",
            Children = [],
            ChildAccelerators = []
        };
        
        var popupNode = new MenuPopupNode
        {
            OwnerNode = ownerNode
        };
        
        var menuItem1 = new MenuItemNode
        {
            Label = "New",
            IsFocused = true  // First item has focus
        };
        var menuItem2 = new MenuItemNode
        {
            Label = "Open",
            IsFocused = false
        };
        
        menuItem1.Parent = popupNode;
        menuItem2.Parent = popupNode;
        popupNode.ChildNodes = [menuItem1, menuItem2];
        
        var zstack = new ZStackNode();
        zstack.Children = [popupNode];
        popupNode.Parent = zstack;
        
        // Build focus ring with both items
        var focusRing = new FocusRing();
        focusRing.Rebuild(zstack);
        
        // Verify initial state
        Assert.Equal(2, focusRing.Focusables.Count);
        Assert.Equal(menuItem1, focusRing.FocusedNode);
        
        // Act - route a down arrow key
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None);
        var state = new InputRouterState();
        
        var result = await InputRouter.RouteInputAsync(zstack, keyEvent, focusRing, state, cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert - focus should have moved to the second item
        Assert.Equal(InputResult.Handled, result);
        Assert.False(menuItem1.IsFocused, "First item should no longer be focused");
        Assert.True(menuItem2.IsFocused, "Second item should now be focused");
        Assert.Equal(menuItem2, focusRing.FocusedNode);
    }
    
    [Fact]
    public void MenuItemNode_Measure_ReturnsCorrectSize()
    {
        var node = new MenuItemNode { Label = "Open", RenderWidth = 20 };
        var constraints = new Constraints(0, 100, 0, 10);
        
        var size = node.Measure(constraints);
        
        Assert.Equal(20, size.Width);
        Assert.Equal(1, size.Height);
    }
    
    [Fact]
    public void MenuItemNode_Measure_UsesLabelLengthWhenNoRenderWidth()
    {
        var node = new MenuItemNode { Label = "Open", RenderWidth = 0 };
        var constraints = new Constraints(0, 100, 0, 10);
        
        var size = node.Measure(constraints);
        
        Assert.Equal(6, size.Width); // "Open" + 2 padding
        Assert.Equal(1, size.Height);
    }
    
    [Fact]
    public void MenuItemNode_IsFocusable_WhenNotDisabled()
    {
        var node = new MenuItemNode { Label = "Open", IsDisabled = false };
        
        Assert.True(node.IsFocusable);
    }
    
    [Fact]
    public void MenuItemNode_NotFocusable_WhenDisabled()
    {
        var node = new MenuItemNode { Label = "Open", IsDisabled = true };
        
        Assert.False(node.IsFocusable);
    }
    
    [Fact]
    public void MenuItemNode_IsFocused_MarksDirty()
    {
        var node = new MenuItemNode { Label = "Open" };
        node.ClearDirty();
        
        node.IsFocused = true;
        
        Assert.True(node.IsDirty);
    }
    
    [Fact]
    public void MenuSeparatorNode_Measure_ReturnsOneRowHeight()
    {
        var node = new MenuSeparatorNode { RenderWidth = 20 };
        var constraints = new Constraints(0, 100, 0, 10);
        
        var size = node.Measure(constraints);
        
        Assert.Equal(20, size.Width);
        Assert.Equal(1, size.Height);
    }
    
    [Fact]
    public void MenuSeparatorNode_NotFocusable()
    {
        var node = new MenuSeparatorNode();
        
        Assert.False(node.IsFocusable);
    }
    
    [Fact]
    public void MenuNode_Measure_ReturnsLabelWidth()
    {
        var node = new MenuNode { Label = "File" };
        var constraints = new Constraints(0, 100, 0, 10);
        
        var size = node.Measure(constraints);
        
        Assert.Equal(6, size.Width); // "File" + 2 padding
        Assert.Equal(1, size.Height);
    }
    
    [Fact]
    public void MenuNode_IsFocusable()
    {
        var node = new MenuNode { Label = "File" };
        
        Assert.True(node.IsFocusable);
    }
    
    [Fact]
    public void MenuNode_ManagesChildFocus()
    {
        var node = new MenuNode { Label = "File" };
        
        Assert.True(node.ManagesChildFocus);
    }
    
    [Fact]
    public void MenuBarNode_ManagesChildFocus()
    {
        var node = new MenuBarNode();
        
        Assert.True(node.ManagesChildFocus);
    }
    
    [Fact]
    public void MenuBarNode_NotFocusable()
    {
        var node = new MenuBarNode();
        
        Assert.False(node.IsFocusable);
    }
    
    [Fact]
    public void BackdropNode_FocusDelegation_SetsChildFocus()
    {
        // Arrange - simulate the tree structure when a menu is open
        // BackdropNode → AnchoredNode → MenuPopupNode → MenuItemNode
        
        var menuItem1 = new MenuItemNode { Label = "New" };
        var menuItem2 = new MenuItemNode { Label = "Open" };
        
        var ownerNode = new MenuNode
        {
            Label = "File",
            Children = [],
            ChildAccelerators = []
        };
        
        var popupNode = new MenuPopupNode { OwnerNode = ownerNode };
        popupNode.ChildNodes = [menuItem1, menuItem2];
        menuItem1.Parent = popupNode;
        menuItem2.Parent = popupNode;
        
        // Simulate AnchoredNode wrapping MenuPopupNode
        var anchoredNode = new AnchoredNode { Child = popupNode };
        popupNode.Parent = anchoredNode;
        
        // Simulate BackdropNode wrapping AnchoredNode
        var backdropNode = new BackdropNode { Child = anchoredNode };
        anchoredNode.Parent = backdropNode;
        
        // Initially, nothing is focused
        Assert.False(menuItem1.IsFocused);
        Assert.False(menuItem2.IsFocused);
        
        // Act - Set focus on BackdropNode (what ZStack does)
        backdropNode.IsFocused = true;
        
        // Assert - Focus should delegate to first focusable child (menuItem1)
        Assert.True(menuItem1.IsFocused);
        Assert.False(menuItem2.IsFocused);
    }
    
    [Fact]
    public async Task InputRouter_EnterKey_ActivatesMenuItem_WhenMenuItemIsFocused()
    {
        // Arrange - simulate full tree with ZStack → BackdropNode → AnchoredNode → MenuPopupNode → MenuItemNode
        var activated = false;
        
        var menuItem1 = new MenuItemNode 
        { 
            Label = "New",
            ActivatedAction = ctx => { activated = true; return Task.CompletedTask; }
        };
        menuItem1.IsFocused = true;  // Directly set focus on the item
        
        var ownerNode = new MenuNode
        {
            Label = "File",
            Children = [],
            ChildAccelerators = []
        };
        
        var popupNode = new MenuPopupNode { OwnerNode = ownerNode };
        popupNode.ChildNodes = [menuItem1];
        menuItem1.Parent = popupNode;
        
        var anchoredNode = new AnchoredNode { Child = popupNode };
        popupNode.Parent = anchoredNode;
        
        var backdropNode = new BackdropNode { Child = anchoredNode };
        anchoredNode.Parent = backdropNode;
        
        var zstack = new ZStackNode();
        zstack.Children = [backdropNode];
        backdropNode.Parent = zstack;
        
        // Build focus ring
        var focusRing = new FocusRing();
        focusRing.Rebuild(zstack);
        
        // Verify menuItem1 is in focus ring and is focused
        Assert.Contains(menuItem1, focusRing.Focusables);
        Assert.Equal(menuItem1, focusRing.FocusedNode);
        
        // Act - Route Enter key
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None);
        var state = new InputRouterState();
        
        var result = await InputRouter.RouteInputAsync(zstack, keyEvent, focusRing, state);
        
        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.True(activated, "ActivatedAction should have been called");
    }
}
