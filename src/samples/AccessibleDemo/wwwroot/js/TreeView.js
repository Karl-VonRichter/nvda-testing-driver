$(function () {
	if ($('.treeview').length) {
		var $tree = $('.treeview ul:first');
		$tree.attr({ 'role': 'tree' });
		var $preselectedElement = $tree.find("a[tabindex='0']");

		//some regularly updated vars depending on nodes expanded/collapsed
		var $allNodes = $('li:visible', $tree);//object with all visible list item nodes
		var lastNodeIdx = $allNodes.length - 1;//last list item node's index
		var $lastNode = $allNodes.eq(lastNodeIdx);//last list item node visible
		function toggleGroup($node) {
			var $toggle = $('> div', $node);
			var $childList = $('> ul', $node);
			$childList.toggle();
			// Update immediately so rapid arrow presses follow the visible tree.
			$allNodes = $('li:visible', $tree);
			lastNodeIdx = $allNodes.length - 1;
			$lastNode = $allNodes.eq(lastNodeIdx);
			if ($toggle.hasClass('collapsed')) {
				$toggle.removeClass('collapsed').addClass('expanded');
				$('> a', $node).attr({ 'aria-expanded': 'true', 'tabindex': '0' }).focus();
			} else {
				$toggle.removeClass('expanded').addClass('collapsed');
				$('> a', $node).attr({ 'aria-expanded': 'false', 'tabindex': '0' }).focus();
			}
		}
		//get next node's link
		function nextNodeLink($el, dir) {
			var thisNodeIdx = $allNodes.index($el.parent());

			if (dir == 'up' || dir == 'parent') {
				var endNodeIdx = 0;
				var operand = -1;
			} else {
				var endNodeIdx = lastNodeIdx;
				var operand = 1;
			}
			if (thisNodeIdx == endNodeIdx) {//if currentNode is last node
				return false; //don't do anything
			}

			if (dir == 'parent') {
				var parentNodeIdx = $allNodes.index($el.parent().parent().parent());
				if (parentNodeIdx < 0) return false;
				var $nextEl = $('> a', $allNodes.eq(parentNodeIdx));
			} else {
				var $nextEl = $('> a', $allNodes.eq(thisNodeIdx + operand));
			}

			$el.attr('tabindex', '-1');
			$nextEl.attr('tabindex', '0').focus();

		}
		//for each link in the tree
		$('li > a', $tree).each(function () {
			var $el = $(this);
			var $node = $el.parent();
			$el.attr({ 'role': 'treeitem', 'aria-selected': 'false', 'tabindex': "-1", 'aria-label': $el.text() });
			$node.attr('role', 'presentation');
			//if children exist
			if ($node.has('ul').length) {
				$node.addClass('hasChildren');
				var $childList = $('ul', $node);
				$childList.attr({ 'role': 'group' }).hide();
				//add toggle element and set aria-expanded on the link
				$('<div aria-hidden="true" class="toggle collapsed">').insertBefore($el);
				$el.attr('aria-expanded', 'false');
			} else {//no children
				$node.addClass('noChildren');
			}
			//set keyboard events
			$el.on('keydown', function (e) {
				if (!(e.shiftKey || e.ctrlKey || e.altKey || e.metaKey)) {
					switch (e.which) {
						case 38: //up
							e.preventDefault();
							nextNodeLink($(this), 'up');
							break;
						case 40: //down
							e.preventDefault();
							nextNodeLink($(this), 'down');
							break;
						case 37: //left
							e.preventDefault();
							if ($(this).attr('aria-expanded') == 'false' || $node.is('.noChildren')) {
								nextNodeLink($(this), 'parent');
							} else {
								toggleGroup($node);
							}
							break;
						case 39: //right
							e.preventDefault();
							if ($(this).attr('aria-expanded') == 'true') {
								nextNodeLink($(this), 'down');
							} else if ($node.is('.hasChildren')) {
								toggleGroup($node);
							}
							break;
					}
				}
			}
			).on('focus', function () {//update aria-selected and the tab stop when focus changes
				$('[role=treeitem]', $tree).attr('tabindex', '-1');
				$(this).attr('tabindex', '0');
				$('[aria-selected="true"]', $tree).attr('aria-selected', 'false');
				$(this).attr('aria-selected', 'true');
			}
			);
		}
		);
		//set tabindex="0" on first link in the tree

		if ($preselectedElement.length == 0) {

			$('> li:first > a', $tree).attr('tabindex', '0');
		}
		else {
			$preselectedElement.attr("tabindex", "0")
				.attr("aria-selected", "true");
			$preselectedElement.parents("li.hasChildren").each(function (index, value) {
				var $iteratingElement = $(this);
				var $ul = $iteratingElement.find("ul:first");
				var $divToggle = $iteratingElement.find("div:first");
				$divToggle.removeClass("collapsed");
				var $linkElement = $iteratingElement.find("a:first");
				$linkElement.attr("aria-expanded", "true");
				$ul.show();
				$allNodes = $('li:visible', $tree);
			});
		}

		//toggle div click and hover
		$('.toggle').on('click',
			function () {
				toggleGroup($(this).parent());
			}
		).hover(
			function () {
				$(this).toggleClass('hover');
			}
		);
		$allNodes = $('li:visible', $tree);//object with all visible list item nodes
		lastNodeIdx = $allNodes.length - 1;//last list item node's index
		$lastNode = $allNodes.eq(lastNodeIdx);//last list item node visible

	}
});