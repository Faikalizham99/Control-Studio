"""Validate extracted counter or direct-Shortcut widget metadata."""
import argparse
import json
import sys
from pathlib import Path
# Also work with the workspace's isolated/embedded Python distribution.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from validate_ipa import validate_direct_shortcut_metadata


def validate(bundle):
    metadata = Path(bundle) / "Metadata.appintents/extract.actionsdata"
    data = json.loads(metadata.read_bytes())
    text = json.dumps(data)
    required_names = ["IncrementControlStudioCounter", "Increment Control Studio Counter"]
    for required in required_names:
        if required not in text:
            raise ValueError(f"Missing App Intents metadata: {required}")
    # The exact metadata format is private and may change; retain it for review.
    # This establishes extraction/target membership, not device discovery or execution.
    print("App Intent metadata contains the expected action identifier and title.")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--widget-trials", action="store_true", help="Check the WidgetKit trial picker instead of the counter action")
    parser.add_argument("bundle", type=Path, help="Path to the extracted extension bundle")
    args = parser.parse_args(argv)
    try:
        if args.widget_trials:
            validate_direct_shortcut_metadata(args.bundle)
            print("Widget App Intent metadata contains the Direct Shortcut picker configuration.")
        else:
            validate(args.bundle)
    except (OSError, ValueError, RecursionError) as error:
        parser.exit(1, f"Metadata validation failed: {error}\n")


if __name__ == "__main__":
    main()
