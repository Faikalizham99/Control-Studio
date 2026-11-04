"""Reject direct replacement of UIKit scroll views' built-in gesture delegates.

Delegate callbacks remain valid on recognizers created and owned by the app.
This source guard catches the access pattern that broke cut-out screen loading;
it does not replace native UIKit tests or C# semantic analysis.
"""
import re

BUILTIN_CALLBACK_ASSIGNMENT = re.compile(
    r"\b(?:PanGestureRecognizer|PinchGestureRecognizer)\s*\.\s*"
    r"(?:Delegate|WeakDelegate|ShouldBegin|ShouldReceiveTouch|ShouldReceivePress|"
    r"ShouldReceiveEvent|ShouldRecognizeSimultaneously|ShouldRequireFailureOf|ShouldBeRequiredToFailBy)\s*(?:\+=|=(?!=))"
)


def validate_builtin_gesture_ownership(source, name="source"):
    if BUILTIN_CALLBACK_ASSIGNMENT.search(source):
        raise ValueError(f"{name}: preserve the scroll view's built-in gesture delegate; use a view override or an app-owned recognizer")
