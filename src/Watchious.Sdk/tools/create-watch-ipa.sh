#!/usr/bin/env bash
# Wraps a signed watch app in the iPhone stub app the App Store needs for a watch-only app, and zips both into an
# .ipa. Xcode builds the same stub from Apple's MessagesApplicationStub (product type
# com.apple.product-type.application.watchapp2-container). Called by Watchious.Sdk.watchOS.targets, which passes:
#   WATCHIOUS_APP             the signed watch app
#   WATCHIOUS_CONTAINER_ID    the stub's bundle identifier (the watch app's without ".watchkitapp")
#   WATCHIOUS_DISPLAY_NAME    the stub's display name
#   WATCHIOUS_VERSION         CFBundleShortVersionString
#   WATCHIOUS_BUILD           CFBundleVersion
#   WATCHIOUS_SIGN_IDENTITY   Apple Distribution identity
#   WATCHIOUS_PROFILE         App Store profile of the stub
#   WATCHIOUS_WORK_DIR        intermediate directory
#   WATCHIOUS_IPA             .ipa to write
set -euo pipefail

APP="${WATCHIOUS_APP%/}"
APP_NAME="$(basename "$APP" .app)"
WORK="$WATCHIOUS_WORK_DIR/ipa"
CONTAINER="$WORK/Payload/$APP_NAME.app"
STUB_DIR="$(xcrun --sdk iphoneos --show-sdk-platform-path)/Library/Application Support/MessagesApplicationStub"
mkdir -p "$CONTAINER/Watch"

lipo "$STUB_DIR/MessagesApplicationStub" -thin arm64 -output "$CONTAINER/$APP_NAME"
sdk_version="$(xcrun --sdk iphoneos --show-sdk-version)"
sdk_build="$(xcrun --sdk iphoneos --show-sdk-build-version)"
xcode_version="$(xcodebuild -version | sed -n 's/^Xcode //p')"
xcode_build="$(xcodebuild -version | sed -n 's/^Build version //p')"
IFS=. read -r xcode_major xcode_minor xcode_patch <<< "$xcode_version"
minimum_ios="$(plutil -extract MinimumOSVersion raw "$APP/Info.plist")"

# The names go into XML, where & and < must be escaped.
xml_text() {
    printf '%s' "$1" | sed -e 's/&/\&amp;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g'
}
display_name="$(xml_text "$WATCHIOUS_DISPLAY_NAME")"
app_name="$(xml_text "$APP_NAME")"

cat > "$CONTAINER/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key><string>en</string>
    <key>CFBundleDisplayName</key><string>$display_name</string>
    <key>CFBundleExecutable</key><string>$app_name</string>
    <key>CFBundleIdentifier</key><string>$WATCHIOUS_CONTAINER_ID</string>
    <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
    <key>CFBundleName</key><string>$app_name</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>$WATCHIOUS_VERSION</string>
    <key>CFBundleVersion</key><string>$WATCHIOUS_BUILD</string>
    <key>CFBundleSupportedPlatforms</key><array><string>iPhoneOS</string></array>
    <key>ITSWatchOnlyContainer</key><true/>
    <key>LSApplicationLaunchProhibited</key><true/>
    <key>LSRequiresIPhoneOS</key><true/>
    <key>MinimumOSVersion</key><string>$minimum_ios</string>
    <key>UIDeviceFamily</key><array><integer>1</integer></array>
    <key>UIRequiredDeviceCapabilities</key><array><string>arm64</string></array>
    <key>DTPlatformName</key><string>iphoneos</string>
    <key>DTPlatformVersion</key><string>$sdk_version</string>
    <key>DTPlatformBuild</key><string>$sdk_build</string>
    <key>DTSDKName</key><string>iphoneos$sdk_version</string>
    <key>DTSDKBuild</key><string>$sdk_build</string>
    <key>DTXcode</key><string>$xcode_major${xcode_minor:-0}${xcode_patch:-0}</string>
    <key>DTXcodeBuild</key><string>$xcode_build</string>
    <key>DTCompiler</key><string>com.apple.compilers.llvm.clang.1_0</string>
    <key>BuildMachineOSBuild</key><string>$(sw_vers -buildVersion)</string>
</dict>
</plist>
EOF
xcrun actool "$STUB_DIR/MessagesApplicationStub.xcassets" --compile "$CONTAINER" --platform iphoneos --target-device iphone \
    --minimum-deployment-target "$minimum_ios" --app-icon MessagesApplicationStub \
    --output-partial-info-plist "$WORK/stub-icon.plist" --output-format human-readable-text --errors > "$WORK/actool.log" \
    || { cat "$WORK/actool.log"; exit 1; }
# The path is quoted inside PlistBuddy's command, which splits at spaces.
/usr/libexec/PlistBuddy -c "Merge '$WORK/stub-icon.plist'" "$CONTAINER/Info.plist" > /dev/null
plutil -convert binary1 "$CONTAINER/Info.plist"
ditto "$APP" "$CONTAINER/Watch/$APP_NAME.app"

cp "$WATCHIOUS_PROFILE" "$CONTAINER/embedded.mobileprovision"
security cms -D -i "$WATCHIOUS_PROFILE" > "$WORK/profile.plist"
cat > "$WORK/stub.entitlements" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>application-identifier</key>
    <string>$(plutil -extract Entitlements.application-identifier raw "$WORK/profile.plist")</string>
    <key>beta-reports-active</key>
    <true/>
    <key>com.apple.developer.team-identifier</key>
    <string>$(plutil -extract TeamIdentifier.0 raw "$WORK/profile.plist")</string>
    <key>get-task-allow</key>
    <false/>
</dict>
</plist>
EOF
codesign --force --sign "$WATCHIOUS_SIGN_IDENTITY" --entitlements "$WORK/stub.entitlements" --generate-entitlement-der \
    "$CONTAINER"
mkdir -p "$(dirname "$WATCHIOUS_IPA")"
ditto -c -k --norsrc --keepParent "$WORK/Payload" "$WATCHIOUS_IPA"
echo "IPA: $WATCHIOUS_IPA"
