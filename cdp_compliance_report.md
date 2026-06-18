# Chrome DevTools Protocol (CDP) Compliance Report

Generated on: 2026-06-18 06:06:24 UTC

This report lists the level of compliance of the `Avalonia.Diagnostics.Cdp` library against the official Chrome DevTools Protocol specification.

## Summary

* **Total Standard CDP Methods Supported**: 112 / 668 (16,8%)
* **Total Custom/Extension Methods Supported**: 29

| Domain | Status / Coverage | Standard Supported | Custom Extensions | Missing Standard |
| :--- | :--- | :--- | :--- | :--- |
| **Accessibility** | 6/8 (75,0%) | 6 | 1 | 2 |
| **Ads** | Unsupported | 0 | 0 | 1 |
| **Animation** | Unsupported | 0 | 0 | 10 |
| **Application** | Custom Domain (3 actions) | 0 | 3 | 0 |
| **Audits** | Unsupported | 0 | 0 | 4 |
| **Autofill** | Unsupported | 0 | 0 | 4 |
| **BackgroundService** | Unsupported | 0 | 0 | 4 |
| **BluetoothEmulation** | Unsupported | 0 | 0 | 15 |
| **Browser** | 5/20 (25,0%) | 5 | 0 | 15 |
| **CacheStorage** | Unsupported | 0 | 0 | 5 |
| **Cast** | Unsupported | 0 | 0 | 6 |
| **Console** | Fully Compliant | 3 | 0 | 0 |
| **CrashReportContext** | Unsupported | 0 | 0 | 1 |
| **CSS** | 7/39 (17,9%) | 7 | 0 | 32 |
| **Debugger** | Unsupported | 0 | 0 | 33 |
| **DeviceAccess** | Unsupported | 0 | 0 | 4 |
| **DeviceOrientation** | Unsupported | 0 | 0 | 2 |
| **DOM** | 20/53 (37,7%) | 20 | 0 | 33 |
| **DOMDebugger** | 3/10 (30,0%) | 3 | 0 | 7 |
| **DOMSnapshot** | Unsupported | 0 | 0 | 4 |
| **DOMStorage** | Unsupported | 0 | 0 | 6 |
| **Emulation** | 9/47 (19,1%) | 9 | 1 | 38 |
| **EventBreakpoints** | Unsupported | 0 | 0 | 3 |
| **Extensions** | Unsupported | 0 | 0 | 8 |
| **FedCm** | Unsupported | 0 | 0 | 7 |
| **Fetch** | Unsupported | 0 | 0 | 9 |
| **FileSystem** | Unsupported | 0 | 0 | 1 |
| **HeadlessExperimental** | Unsupported | 0 | 0 | 3 |
| **HeapProfiler** | Unsupported | 0 | 0 | 12 |
| **IndexedDB** | Unsupported | 0 | 0 | 9 |
| **Input** | 3/13 (23,1%) | 3 | 18 | 10 |
| **Inspector** | Unsupported | 0 | 0 | 2 |
| **IO** | Unsupported | 0 | 0 | 3 |
| **LayerTree** | Unsupported | 0 | 0 | 9 |
| **Log** | Fully Compliant | 5 | 0 | 0 |
| **Media** | Unsupported | 0 | 0 | 2 |
| **Memory** | 1/11 (9,1%) | 1 | 2 | 10 |
| **Network** | 10/41 (24,4%) | 10 | 0 | 31 |
| **Overlay** | 6/30 (20,0%) | 6 | 0 | 24 |
| **Page** | 12/61 (19,7%) | 12 | 0 | 49 |
| **Performance** | Fully Compliant | 4 | 0 | 0 |
| **PerformanceTimeline** | Unsupported | 0 | 0 | 1 |
| **Preload** | Unsupported | 0 | 0 | 2 |
| **Profiler** | Unsupported | 0 | 0 | 9 |
| **PWA** | Unsupported | 0 | 0 | 7 |
| **Recorder** | Custom Domain (2 actions) | 0 | 2 | 0 |
| **Runtime** | 10/23 (43,5%) | 10 | 0 | 13 |
| **Schema** | Fully Compliant | 1 | 0 | 0 |
| **Security** | Unsupported | 0 | 0 | 5 |
| **ServiceWorker** | Unsupported | 0 | 0 | 12 |
| **SmartCardEmulation** | Unsupported | 0 | 0 | 12 |
| **Sources** | Custom Domain (2 actions) | 0 | 2 | 0 |
| **Storage** | Unsupported | 0 | 0 | 34 |
| **SystemInfo** | Fully Compliant | 3 | 0 | 0 |
| **Target** | 4/19 (21,1%) | 4 | 0 | 15 |
| **Tethering** | Unsupported | 0 | 0 | 2 |
| **Tracing** | Unsupported | 0 | 0 | 6 |
| **WebAudio** | Unsupported | 0 | 0 | 3 |
| **WebAuthn** | Unsupported | 0 | 0 | 13 |
| **WebMCP** | Unsupported | 0 | 0 | 4 |

## Domain Details

### Accessibility

* **Standard Supported (6)**: `disable`, `enable`, `getAXNodeAndAncestors`, `getChildAXNodes`, `getFullAXTree`, `getRootAXNode`
* **Custom Extensions (1)**: `getAXNode`
* **Missing Standard (2)**: `getPartialAXTree`, `queryAXTree`

### Ads

* **Missing Standard (1)**: `getAdMetrics`

### Animation

* **Missing Standard (10)**: `disable`, `enable`, `getCurrentTime`, `getPlaybackRate`, `releaseAnimations`, `resolveAnimation`, `seekAnimations`, `setPaused`, `setPlaybackRate`, `setTiming`

### Application

* **Custom Extensions (3)**: `deleteResource`, `getResources`, `setResource`

### Audits

* **Missing Standard (4)**: `checkFormsIssues`, `disable`, `enable`, `getEncodedResponse`

### Autofill

* **Missing Standard (4)**: `disable`, `enable`, `setAddresses`, `trigger`

### BackgroundService

* **Missing Standard (4)**: `clearEvents`, `setRecording`, `startObserving`, `stopObserving`

### BluetoothEmulation

* **Missing Standard (15)**: `addCharacteristic`, `addDescriptor`, `addService`, `disable`, `enable`, `removeCharacteristic`, `removeDescriptor`, `removeService`, `setSimulatedCentralState`, `simulateAdvertisement`, `simulateCharacteristicOperationResponse`, `simulateDescriptorOperationResponse`, `simulateGATTDisconnection`, `simulateGATTOperationResponse`, `simulatePreconnectedPeripheral`

### Browser

* **Standard Supported (5)**: `close`, `getVersion`, `getWindowBounds`, `getWindowForTarget`, `setWindowBounds`
* **Missing Standard (15)**: `addPrivacySandboxCoordinatorKeyConfig`, `addPrivacySandboxEnrollmentOverride`, `cancelDownload`, `crash`, `crashGpuProcess`, `executeBrowserCommand`, `getBrowserCommandLine`, `getHistogram`, `getHistograms`, `grantPermissions`, `resetPermissions`, `setContentsSize`, `setDockTile`, `setDownloadBehavior`, `setPermission`

### CacheStorage

* **Missing Standard (5)**: `deleteCache`, `deleteEntry`, `requestCachedResponse`, `requestCacheNames`, `requestEntries`

### Cast

* **Missing Standard (6)**: `disable`, `enable`, `setSinkToUse`, `startDesktopMirroring`, `startTabMirroring`, `stopCasting`

### Console

* **Standard Supported (3)**: `clearMessages`, `disable`, `enable`

### CrashReportContext

* **Missing Standard (1)**: `getEntries`

### CSS

* **Standard Supported (7)**: `createStyleSheet`, `disable`, `enable`, `getComputedStyleForNode`, `getMatchedStylesForNode`, `setStyleSheetText`, `setStyleTexts`
* **Missing Standard (32)**: `addRule`, `collectClassNames`, `forcePseudoState`, `forceStartingStyle`, `getAnimatedStylesForNode`, `getBackgroundColors`, `getEnvironmentVariables`, `getInlineStylesForNode`, `getLayersForNode`, `getLocationForSelector`, `getLonghandProperties`, `getMediaQueries`, `getPlatformFontsForNode`, `getStyleSheetText`, `resolveValues`, `setContainerQueryConditionText`, `setContainerQueryText`, `setEffectivePropertyValueForNode`, `setKeyframeKey`, `setLocalFontsEnabled`, `setMediaText`, `setNavigationText`, `setPropertyRulePropertyName`, `setRuleSelector`, `setScopeText`, `setSupportsText`, `startRuleUsageTracking`, `stopRuleUsageTracking`, `takeComputedStyleUpdates`, `takeCoverageDelta`, `trackComputedStyleUpdates`, `trackComputedStyleUpdatesForNode`

### Debugger

* **Missing Standard (33)**: `continueToLocation`, `disable`, `disassembleWasmModule`, `enable`, `evaluateOnCallFrame`, `getPossibleBreakpoints`, `getScriptSource`, `getStackTrace`, `getWasmBytecode`, `nextWasmDisassemblyChunk`, `pause`, `pauseOnAsyncCall`, `removeBreakpoint`, `restartFrame`, `resume`, `searchInContent`, `setAsyncCallStackDepth`, `setBlackboxedRanges`, `setBlackboxExecutionContexts`, `setBlackboxPatterns`, `setBreakpoint`, `setBreakpointByUrl`, `setBreakpointOnFunctionCall`, `setBreakpointsActive`, `setInstrumentationBreakpoint`, `setPauseOnExceptions`, `setReturnValue`, `setScriptSource`, `setSkipAllPauses`, `setVariableValue`, `stepInto`, `stepOut`, `stepOver`

### DeviceAccess

* **Missing Standard (4)**: `cancelPrompt`, `disable`, `enable`, `selectPrompt`

### DeviceOrientation

* **Missing Standard (2)**: `clearDeviceOrientationOverride`, `setDeviceOrientationOverride`

### DOM

* **Standard Supported (20)**: `describeNode`, `disable`, `discardSearchResults`, `enable`, `focus`, `getAttributes`, `getBoxModel`, `getDocument`, `getNodeForLocation`, `getOuterHTML`, `getSearchResults`, `performSearch`, `querySelector`, `querySelectorAll`, `removeAttribute`, `removeNode`, `requestChildNodes`, `resolveNode`, `setAttributeValue`, `setInspectedNode`
* **Missing Standard (33)**: `collectClassNamesFromSubtree`, `copyTo`, `forceShowPopover`, `getAnchorElement`, `getContainerForNode`, `getContentQuads`, `getDetachedDomNodes`, `getElementByRelation`, `getFileInfo`, `getFlattenedDocument`, `getFrameOwner`, `getNodesForSubtreeByStyle`, `getNodeStackTraces`, `getQueryingDescendantsForContainer`, `getRelayoutBoundary`, `getTopLayerElements`, `hideHighlight`, `highlightNode`, `highlightRect`, `markUndoableState`, `moveTo`, `pushNodeByPathToFrontend`, `pushNodesByBackendIdsToFrontend`, `redo`, `requestNode`, `scrollIntoViewIfNeeded`, `setAttributesAsText`, `setFileInputFiles`, `setNodeName`, `setNodeStackTracesEnabled`, `setNodeValue`, `setOuterHTML`, `undo`

### DOMDebugger

* **Standard Supported (3)**: `getEventListeners`, `removeEventListenerBreakpoint`, `setEventListenerBreakpoint`
* **Missing Standard (7)**: `removeDOMBreakpoint`, `removeInstrumentationBreakpoint`, `removeXHRBreakpoint`, `setBreakOnCSPViolation`, `setDOMBreakpoint`, `setInstrumentationBreakpoint`, `setXHRBreakpoint`

### DOMSnapshot

* **Missing Standard (4)**: `captureSnapshot`, `disable`, `enable`, `getSnapshot`

### DOMStorage

* **Missing Standard (6)**: `clear`, `disable`, `enable`, `getDOMStorageItems`, `removeDOMStorageItem`, `setDOMStorageItem`

### Emulation

* **Standard Supported (9)**: `canEmulate`, `clearDeviceMetricsOverride`, `setAutoDarkModeOverride`, `setCPUThrottlingRate`, `setDeviceMetricsOverride`, `setEmulatedMedia`, `setFocusEmulationEnabled`, `setLocaleOverride`, `setTouchEmulationEnabled`
* **Custom Extensions (1)**: `setEmulatedColorSchemeOverride`
* **Missing Standard (38)**: `addScreen`, `clearDevicePostureOverride`, `clearDisplayFeaturesOverride`, `clearGeolocationOverride`, `clearIdleOverride`, `getOverriddenSensorInformation`, `getScreenInfos`, `removeScreen`, `resetPageScaleFactor`, `setAutomationOverride`, `setDataSaverOverride`, `setDefaultBackgroundColorOverride`, `setDevicePostureOverride`, `setDisabledImageTypes`, `setDisplayFeaturesOverride`, `setDocumentCookieDisabled`, `setEmitTouchEventsForMouse`, `setEmulatedOSTextScale`, `setEmulatedVisionDeficiency`, `setGeolocationOverride`, `setHardwareConcurrencyOverride`, `setIdleOverride`, `setNavigatorOverrides`, `setPageScaleFactor`, `setPressureSourceOverrideEnabled`, `setPressureStateOverride`, `setPrimaryScreen`, `setSafeAreaInsetsOverride`, `setScriptExecutionDisabled`, `setScrollbarsHidden`, `setSensorOverrideEnabled`, `setSensorOverrideReadings`, `setSmallViewportHeightDifferenceOverride`, `setTimezoneOverride`, `setUserAgentOverride`, `setVirtualTimePolicy`, `setVisibleSize`, `updateScreen`

### EventBreakpoints

* **Missing Standard (3)**: `disable`, `removeInstrumentationBreakpoint`, `setInstrumentationBreakpoint`

### Extensions

* **Missing Standard (8)**: `clearStorageItems`, `getExtensions`, `getStorageItems`, `loadUnpacked`, `removeStorageItems`, `setStorageItems`, `triggerAction`, `uninstall`

### FedCm

* **Missing Standard (7)**: `clickDialogButton`, `disable`, `dismissDialog`, `enable`, `openUrl`, `resetCooldown`, `selectAccount`

### Fetch

* **Missing Standard (9)**: `continueRequest`, `continueResponse`, `continueWithAuth`, `disable`, `enable`, `failRequest`, `fulfillRequest`, `getResponseBody`, `takeResponseBodyAsStream`

### FileSystem

* **Missing Standard (1)**: `getDirectory`

### HeadlessExperimental

* **Missing Standard (3)**: `beginFrame`, `disable`, `enable`

### HeapProfiler

* **Missing Standard (12)**: `addInspectedHeapObject`, `collectGarbage`, `disable`, `enable`, `getHeapObjectId`, `getObjectByHeapObjectId`, `getSamplingProfile`, `startSampling`, `startTrackingHeapObjects`, `stopSampling`, `stopTrackingHeapObjects`, `takeHeapSnapshot`

### IndexedDB

* **Missing Standard (9)**: `clearObjectStore`, `deleteDatabase`, `deleteObjectStoreEntries`, `disable`, `enable`, `getMetadata`, `requestData`, `requestDatabase`, `requestDatabaseNames`

### Input

* **Standard Supported (3)**: `dispatchKeyEvent`, `dispatchMouseEvent`, `insertText`
* **Custom Extensions (18)**: `arrowdown`, `arrowleft`, `arrowright`, `arrowup`, `backspace`, `delete`, `down`, `end`, `enter`, `escape`, `home`, `left`, `pagedown`, `pageup`, `right`, `space`, `tab`, `up`
* **Missing Standard (10)**: `cancelDragging`, `dispatchDragEvent`, `dispatchTouchEvent`, `emulateTouchFromMouseEvent`, `imeSetComposition`, `setIgnoreInputEvents`, `setInterceptDrags`, `synthesizePinchGesture`, `synthesizeScrollGesture`, `synthesizeTapGesture`

### Inspector

* **Missing Standard (2)**: `disable`, `enable`

### IO

* **Missing Standard (3)**: `close`, `read`, `resolveBlob`

### LayerTree

* **Missing Standard (9)**: `compositingReasons`, `disable`, `enable`, `loadSnapshot`, `makeSnapshot`, `profileSnapshot`, `releaseSnapshot`, `replaySnapshot`, `snapshotCommandLog`

### Log

* **Standard Supported (5)**: `clear`, `disable`, `enable`, `startViolationsReport`, `stopViolationsReport`

### Media

* **Missing Standard (2)**: `disable`, `enable`

### Memory

* **Standard Supported (1)**: `getDOMCounters`
* **Custom Extensions (2)**: `collectGarbage`, `getLiveControls`
* **Missing Standard (10)**: `forciblyPurgeJavaScriptMemory`, `getAllTimeSamplingProfile`, `getBrowserSamplingProfile`, `getDOMCountersForLeakDetection`, `getSamplingProfile`, `prepareForLeakDetection`, `setPressureNotificationsSuppressed`, `simulatePressureNotification`, `startSampling`, `stopSampling`

### Network

* **Standard Supported (10)**: `canClearBrowserCache`, `canClearBrowserCookies`, `canEmulateNetworkConditions`, `clearBrowserCache`, `clearBrowserCookies`, `disable`, `emulateNetworkConditions`, `enable`, `getResponseBody`, `setCacheDisabled`
* **Missing Standard (31)**: `clearAcceptedEncodingsOverride`, `configureDurableMessages`, `continueInterceptedRequest`, `deleteCookies`, `deleteDeviceBoundSession`, `emulateNetworkConditionsByRule`, `enableDeviceBoundSessions`, `enableReportingApi`, `fetchSchemefulSite`, `getAllCookies`, `getCertificate`, `getCookies`, `getRequestPostData`, `getResponseBodyForInterception`, `getSecurityIsolationStatus`, `loadNetworkResource`, `overrideNetworkState`, `replayXHR`, `searchInResponseBody`, `setAcceptedEncodings`, `setAttachDebugStack`, `setBlockedURLs`, `setBypassServiceWorker`, `setCookie`, `setCookieControls`, `setCookies`, `setExtraHTTPHeaders`, `setRequestInterception`, `setUserAgentOverride`, `streamResourceContent`, `takeResponseBodyForInterceptionAsStream`

### Overlay

* **Standard Supported (6)**: `disable`, `enable`, `hideHighlight`, `highlightNode`, `highlightRect`, `setInspectMode`
* **Missing Standard (24)**: `getGridHighlightObjectsForTest`, `getHighlightObjectForTest`, `getSourceOrderHighlightObjectForTest`, `highlightFrame`, `highlightQuad`, `highlightSourceOrder`, `setPausedInDebuggerMessage`, `setShowAdHighlights`, `setShowContainerQueryOverlays`, `setShowDebugBorders`, `setShowFlexOverlays`, `setShowFPSCounter`, `setShowGridOverlays`, `setShowHinge`, `setShowHitTestBorders`, `setShowInspectedElementAnchor`, `setShowIsolatedElements`, `setShowLayoutShiftRegions`, `setShowPaintRects`, `setShowScrollBottleneckRects`, `setShowScrollSnapOverlays`, `setShowViewportSizeOnResize`, `setShowWebVitals`, `setShowWindowControlsOverlay`

### Page

* **Standard Supported (12)**: `bringToFront`, `captureScreenshot`, `disable`, `enable`, `getLayoutMetrics`, `getResourceContent`, `getResourceTree`, `navigate`, `reload`, `screencastFrameAck`, `startScreencast`, `stopScreencast`
* **Missing Standard (49)**: `addCompilationCache`, `addScriptToEvaluateOnLoad`, `addScriptToEvaluateOnNewDocument`, `captureSnapshot`, `clearCompilationCache`, `clearDeviceMetricsOverride`, `clearDeviceOrientationOverride`, `clearGeolocationOverride`, `close`, `crash`, `createIsolatedWorld`, `deleteCookie`, `generateTestReport`, `getAdScriptAncestry`, `getAnnotatedPageContent`, `getAppId`, `getAppManifest`, `getFrameTree`, `getInstallabilityErrors`, `getManifestIcons`, `getNavigationHistory`, `getOriginTrials`, `getPermissionsPolicyState`, `handleJavaScriptDialog`, `navigateToHistoryEntry`, `printToPDF`, `produceCompilationCache`, `removeScriptToEvaluateOnLoad`, `removeScriptToEvaluateOnNewDocument`, `resetNavigationHistory`, `searchInResource`, `setAdBlockingEnabled`, `setBypassCSP`, `setDeviceMetricsOverride`, `setDeviceOrientationOverride`, `setDocumentContent`, `setDownloadBehavior`, `setFontFamilies`, `setFontSizes`, `setGeolocationOverride`, `setInterceptFileChooserDialog`, `setLifecycleEventsEnabled`, `setPrerenderingAllowed`, `setRPHRegistrationMode`, `setSPCTransactionMode`, `setTouchEmulationEnabled`, `setWebLifecycleState`, `stopLoading`, `waitForDebugger`

### Performance

* **Standard Supported (4)**: `disable`, `enable`, `getMetrics`, `setTimeDomain`

### PerformanceTimeline

* **Missing Standard (1)**: `enable`

### Preload

* **Missing Standard (2)**: `disable`, `enable`

### Profiler

* **Missing Standard (9)**: `disable`, `enable`, `getBestEffortCoverage`, `setSamplingInterval`, `start`, `startPreciseCoverage`, `stop`, `stopPreciseCoverage`, `takePreciseCoverage`

### PWA

* **Missing Standard (7)**: `changeAppUserSettings`, `getOsAppState`, `install`, `launch`, `launchFilesInApp`, `openCurrentPageInApp`, `uninstall`

### Recorder

* **Custom Extensions (2)**: `start`, `stop`

### Runtime

* **Standard Supported (10)**: `callFunctionOn`, `disable`, `discardConsoleEntries`, `enable`, `evaluate`, `getHeapUsage`, `getIsolateId`, `getProperties`, `releaseObject`, `releaseObjectGroup`
* **Missing Standard (13)**: `addBinding`, `awaitPromise`, `compileScript`, `getExceptionDetails`, `globalLexicalScopeNames`, `queryObjects`, `removeBinding`, `runIfWaitingForDebugger`, `runScript`, `setAsyncCallStackDepth`, `setCustomObjectFormatterEnabled`, `setMaxCallStackSizeToCapture`, `terminateExecution`

### Schema

* **Standard Supported (1)**: `getDomains`

### Security

* **Missing Standard (5)**: `disable`, `enable`, `handleCertificateError`, `setIgnoreCertificateErrors`, `setOverrideCertificateErrors`

### ServiceWorker

* **Missing Standard (12)**: `deliverPushMessage`, `disable`, `dispatchPeriodicSyncEvent`, `dispatchSyncEvent`, `enable`, `setForceUpdateOnPageLoad`, `skipWaiting`, `startWorker`, `stopAllWorkers`, `stopWorker`, `unregister`, `updateRegistration`

### SmartCardEmulation

* **Missing Standard (12)**: `disable`, `enable`, `reportBeginTransactionResult`, `reportConnectResult`, `reportDataResult`, `reportError`, `reportEstablishContextResult`, `reportGetStatusChangeResult`, `reportListReadersResult`, `reportPlainResult`, `reportReleaseContextResult`, `reportStatusResult`

### Sources

* **Custom Extensions (2)**: `getFileContent`, `getWorkspaceFiles`

### Storage

* **Missing Standard (34)**: `clearCookies`, `clearDataForOrigin`, `clearDataForStorageKey`, `clearSharedStorageEntries`, `clearTrustTokens`, `deleteSharedStorageEntry`, `deleteStorageBucket`, `getCookies`, `getInterestGroupDetails`, `getRelatedWebsiteSets`, `getSharedStorageEntries`, `getSharedStorageMetadata`, `getStorageKey`, `getStorageKeyForFrame`, `getTrustTokens`, `getUsageAndQuota`, `overrideQuotaForOrigin`, `resetSharedStorageBudget`, `runBounceTrackingMitigations`, `setCookies`, `setInterestGroupAuctionTracking`, `setInterestGroupTracking`, `setProtectedAudienceKAnonymity`, `setSharedStorageEntry`, `setSharedStorageTracking`, `setStorageBucketTracking`, `trackCacheStorageForOrigin`, `trackCacheStorageForStorageKey`, `trackIndexedDBForOrigin`, `trackIndexedDBForStorageKey`, `untrackCacheStorageForOrigin`, `untrackCacheStorageForStorageKey`, `untrackIndexedDBForOrigin`, `untrackIndexedDBForStorageKey`

### SystemInfo

* **Standard Supported (3)**: `getFeatureState`, `getInfo`, `getProcessInfo`

### Target

* **Standard Supported (4)**: `getTargetInfo`, `getTargets`, `setAutoAttach`, `setDiscoverTargets`
* **Missing Standard (15)**: `activateTarget`, `attachToBrowserTarget`, `attachToTarget`, `autoAttachRelated`, `closeTarget`, `createBrowserContext`, `createTarget`, `detachFromTarget`, `disposeBrowserContext`, `exposeDevToolsProtocol`, `getBrowserContexts`, `getDevToolsTarget`, `openDevTools`, `sendMessageToTarget`, `setRemoteLocations`

### Tethering

* **Missing Standard (2)**: `bind`, `unbind`

### Tracing

* **Missing Standard (6)**: `end`, `getCategories`, `getTrackEventDescriptor`, `recordClockSyncMarker`, `requestMemoryDump`, `start`

### WebAudio

* **Missing Standard (3)**: `disable`, `enable`, `getRealtimeData`

### WebAuthn

* **Missing Standard (13)**: `addCredential`, `addVirtualAuthenticator`, `clearCredentials`, `disable`, `enable`, `getCredential`, `getCredentials`, `removeCredential`, `removeVirtualAuthenticator`, `setAutomaticPresenceSimulation`, `setCredentialProperties`, `setResponseOverrideBits`, `setUserVerified`

### WebMCP

* **Missing Standard (4)**: `cancelInvocation`, `disable`, `enable`, `invokeTool`

