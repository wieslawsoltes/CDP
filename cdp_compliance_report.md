# Chrome DevTools Protocol (CDP) Compliance Report

Generated on: 2026-06-18 08:29:11 UTC

This report lists the level of compliance of the `Avalonia.Diagnostics.Cdp` library against the official Chrome DevTools Protocol specification.

## Summary

* **Total Standard CDP Methods Supported**: 254 / 668 (38,0%)
* **Total Custom/Extension Methods Supported**: 11

| Domain | Status / Coverage | Standard Supported | Custom Extensions | Missing Standard |
| :--- | :--- | :--- | :--- | :--- |
| **Accessibility** | Fully Compliant | 8 | 1 | 0 |
| **Ads** | Fully Compliant | 1 | 0 | 0 |
| **Animation** | Unsupported | 0 | 0 | 10 |
| **Application** | Custom Domain (3 actions) | 0 | 3 | 0 |
| **Audits** | Fully Compliant | 4 | 0 | 0 |
| **Autofill** | Fully Compliant | 4 | 0 | 0 |
| **BackgroundService** | Fully Compliant | 4 | 0 | 0 |
| **BluetoothEmulation** | Unsupported | 0 | 0 | 15 |
| **Browser** | 12/20 (60,0%) | 12 | 0 | 8 |
| **CacheStorage** | Unsupported | 0 | 0 | 5 |
| **Cast** | Fully Compliant | 6 | 0 | 0 |
| **Console** | Fully Compliant | 3 | 0 | 0 |
| **CrashReportContext** | Fully Compliant | 1 | 0 | 0 |
| **CSS** | 7/39 (17,9%) | 7 | 0 | 32 |
| **Debugger** | Unsupported | 0 | 0 | 33 |
| **DeviceAccess** | Fully Compliant | 4 | 0 | 0 |
| **DeviceOrientation** | Fully Compliant | 2 | 0 | 0 |
| **DOM** | 26/53 (49,1%) | 26 | 0 | 27 |
| **DOMDebugger** | Fully Compliant | 10 | 0 | 0 |
| **DOMSnapshot** | Fully Compliant | 4 | 0 | 0 |
| **DOMStorage** | Fully Compliant | 6 | 0 | 0 |
| **Emulation** | 21/47 (44,7%) | 21 | 1 | 26 |
| **EventBreakpoints** | Fully Compliant | 3 | 0 | 0 |
| **Extensions** | Unsupported | 0 | 0 | 8 |
| **FedCm** | Unsupported | 0 | 0 | 7 |
| **Fetch** | Unsupported | 0 | 0 | 9 |
| **FileSystem** | Fully Compliant | 1 | 0 | 0 |
| **HeadlessExperimental** | Unsupported | 0 | 0 | 3 |
| **HeapProfiler** | Unsupported | 0 | 0 | 12 |
| **IndexedDB** | Unsupported | 0 | 0 | 9 |
| **Input** | 7/13 (53,8%) | 7 | 0 | 6 |
| **Inspector** | Fully Compliant | 2 | 0 | 0 |
| **IO** | Unsupported | 0 | 0 | 3 |
| **LayerTree** | Unsupported | 0 | 0 | 9 |
| **Log** | Fully Compliant | 5 | 0 | 0 |
| **Media** | Fully Compliant | 2 | 0 | 0 |
| **Memory** | 5/11 (45,5%) | 5 | 2 | 6 |
| **Network** | 21/41 (51,2%) | 21 | 0 | 20 |
| **Overlay** | 13/30 (43,3%) | 13 | 0 | 17 |
| **Page** | 29/61 (47,5%) | 29 | 0 | 32 |
| **Performance** | Fully Compliant | 4 | 0 | 0 |
| **PerformanceTimeline** | Fully Compliant | 1 | 0 | 0 |
| **Preload** | Fully Compliant | 2 | 0 | 0 |
| **Profiler** | Unsupported | 0 | 0 | 9 |
| **PWA** | Unsupported | 0 | 0 | 7 |
| **Recorder** | Custom Domain (2 actions) | 0 | 2 | 0 |
| **Runtime** | 16/23 (69,6%) | 16 | 0 | 7 |
| **Schema** | Fully Compliant | 1 | 0 | 0 |
| **Security** | Unsupported | 0 | 0 | 5 |
| **ServiceWorker** | Unsupported | 0 | 0 | 12 |
| **SmartCardEmulation** | Unsupported | 0 | 0 | 12 |
| **Sources** | Custom Domain (2 actions) | 0 | 2 | 0 |
| **Storage** | Unsupported | 0 | 0 | 34 |
| **SystemInfo** | Fully Compliant | 3 | 0 | 0 |
| **Target** | 11/19 (57,9%) | 11 | 0 | 8 |
| **Tethering** | Fully Compliant | 2 | 0 | 0 |
| **Tracing** | Unsupported | 0 | 0 | 6 |
| **WebAudio** | Fully Compliant | 3 | 0 | 0 |
| **WebAuthn** | Unsupported | 0 | 0 | 13 |
| **WebMCP** | Unsupported | 0 | 0 | 4 |

## Domain Details

### Accessibility

* **Standard Supported (8)**: `disable`, `enable`, `getAXNodeAndAncestors`, `getChildAXNodes`, `getFullAXTree`, `getPartialAXTree`, `getRootAXNode`, `queryAXTree`
* **Custom Extensions (1)**: `getAXNode`

### Ads

* **Standard Supported (1)**: `getAdMetrics`

### Animation

* **Missing Standard (10)**: `disable`, `enable`, `getCurrentTime`, `getPlaybackRate`, `releaseAnimations`, `resolveAnimation`, `seekAnimations`, `setPaused`, `setPlaybackRate`, `setTiming`

### Application

* **Custom Extensions (3)**: `deleteResource`, `getResources`, `setResource`

### Audits

* **Standard Supported (4)**: `checkFormsIssues`, `disable`, `enable`, `getEncodedResponse`

### Autofill

* **Standard Supported (4)**: `disable`, `enable`, `setAddresses`, `trigger`

### BackgroundService

* **Standard Supported (4)**: `clearEvents`, `setRecording`, `startObserving`, `stopObserving`

### BluetoothEmulation

* **Missing Standard (15)**: `addCharacteristic`, `addDescriptor`, `addService`, `disable`, `enable`, `removeCharacteristic`, `removeDescriptor`, `removeService`, `setSimulatedCentralState`, `simulateAdvertisement`, `simulateCharacteristicOperationResponse`, `simulateDescriptorOperationResponse`, `simulateGATTDisconnection`, `simulateGATTOperationResponse`, `simulatePreconnectedPeripheral`

### Browser

* **Standard Supported (12)**: `cancelDownload`, `close`, `crash`, `getBrowserCommandLine`, `getVersion`, `getWindowBounds`, `getWindowForTarget`, `grantPermissions`, `resetPermissions`, `setDownloadBehavior`, `setPermission`, `setWindowBounds`
* **Missing Standard (8)**: `addPrivacySandboxCoordinatorKeyConfig`, `addPrivacySandboxEnrollmentOverride`, `crashGpuProcess`, `executeBrowserCommand`, `getHistogram`, `getHistograms`, `setContentsSize`, `setDockTile`

### CacheStorage

* **Missing Standard (5)**: `deleteCache`, `deleteEntry`, `requestCachedResponse`, `requestCacheNames`, `requestEntries`

### Cast

* **Standard Supported (6)**: `disable`, `enable`, `setSinkToUse`, `startDesktopMirroring`, `startTabMirroring`, `stopCasting`

### Console

* **Standard Supported (3)**: `clearMessages`, `disable`, `enable`

### CrashReportContext

* **Standard Supported (1)**: `getEntries`

### CSS

* **Standard Supported (7)**: `createStyleSheet`, `disable`, `enable`, `getComputedStyleForNode`, `getMatchedStylesForNode`, `setStyleSheetText`, `setStyleTexts`
* **Missing Standard (32)**: `addRule`, `collectClassNames`, `forcePseudoState`, `forceStartingStyle`, `getAnimatedStylesForNode`, `getBackgroundColors`, `getEnvironmentVariables`, `getInlineStylesForNode`, `getLayersForNode`, `getLocationForSelector`, `getLonghandProperties`, `getMediaQueries`, `getPlatformFontsForNode`, `getStyleSheetText`, `resolveValues`, `setContainerQueryConditionText`, `setContainerQueryText`, `setEffectivePropertyValueForNode`, `setKeyframeKey`, `setLocalFontsEnabled`, `setMediaText`, `setNavigationText`, `setPropertyRulePropertyName`, `setRuleSelector`, `setScopeText`, `setSupportsText`, `startRuleUsageTracking`, `stopRuleUsageTracking`, `takeComputedStyleUpdates`, `takeCoverageDelta`, `trackComputedStyleUpdates`, `trackComputedStyleUpdatesForNode`

### Debugger

* **Missing Standard (33)**: `continueToLocation`, `disable`, `disassembleWasmModule`, `enable`, `evaluateOnCallFrame`, `getPossibleBreakpoints`, `getScriptSource`, `getStackTrace`, `getWasmBytecode`, `nextWasmDisassemblyChunk`, `pause`, `pauseOnAsyncCall`, `removeBreakpoint`, `restartFrame`, `resume`, `searchInContent`, `setAsyncCallStackDepth`, `setBlackboxedRanges`, `setBlackboxExecutionContexts`, `setBlackboxPatterns`, `setBreakpoint`, `setBreakpointByUrl`, `setBreakpointOnFunctionCall`, `setBreakpointsActive`, `setInstrumentationBreakpoint`, `setPauseOnExceptions`, `setReturnValue`, `setScriptSource`, `setSkipAllPauses`, `setVariableValue`, `stepInto`, `stepOut`, `stepOver`

### DeviceAccess

* **Standard Supported (4)**: `cancelPrompt`, `disable`, `enable`, `selectPrompt`

### DeviceOrientation

* **Standard Supported (2)**: `clearDeviceOrientationOverride`, `setDeviceOrientationOverride`

### DOM

* **Standard Supported (26)**: `describeNode`, `disable`, `discardSearchResults`, `enable`, `focus`, `getAttributes`, `getBoxModel`, `getDocument`, `getFlattenedDocument`, `getNodeForLocation`, `getOuterHTML`, `getSearchResults`, `performSearch`, `querySelector`, `querySelectorAll`, `removeAttribute`, `removeNode`, `requestChildNodes`, `requestNode`, `resolveNode`, `scrollIntoViewIfNeeded`, `setAttributeValue`, `setInspectedNode`, `setNodeName`, `setNodeValue`, `setOuterHTML`
* **Missing Standard (27)**: `collectClassNamesFromSubtree`, `copyTo`, `forceShowPopover`, `getAnchorElement`, `getContainerForNode`, `getContentQuads`, `getDetachedDomNodes`, `getElementByRelation`, `getFileInfo`, `getFrameOwner`, `getNodesForSubtreeByStyle`, `getNodeStackTraces`, `getQueryingDescendantsForContainer`, `getRelayoutBoundary`, `getTopLayerElements`, `hideHighlight`, `highlightNode`, `highlightRect`, `markUndoableState`, `moveTo`, `pushNodeByPathToFrontend`, `pushNodesByBackendIdsToFrontend`, `redo`, `setAttributesAsText`, `setFileInputFiles`, `setNodeStackTracesEnabled`, `undo`

### DOMDebugger

* **Standard Supported (10)**: `getEventListeners`, `removeDOMBreakpoint`, `removeEventListenerBreakpoint`, `removeInstrumentationBreakpoint`, `removeXHRBreakpoint`, `setBreakOnCSPViolation`, `setDOMBreakpoint`, `setEventListenerBreakpoint`, `setInstrumentationBreakpoint`, `setXHRBreakpoint`

### DOMSnapshot

* **Standard Supported (4)**: `captureSnapshot`, `disable`, `enable`, `getSnapshot`

### DOMStorage

* **Standard Supported (6)**: `clear`, `disable`, `enable`, `getDOMStorageItems`, `removeDOMStorageItem`, `setDOMStorageItem`

### Emulation

* **Standard Supported (21)**: `canEmulate`, `clearDeviceMetricsOverride`, `clearGeolocationOverride`, `clearIdleOverride`, `setAutoDarkModeOverride`, `setCPUThrottlingRate`, `setDefaultBackgroundColorOverride`, `setDeviceMetricsOverride`, `setDocumentCookieDisabled`, `setEmitTouchEventsForMouse`, `setEmulatedMedia`, `setFocusEmulationEnabled`, `setGeolocationOverride`, `setIdleOverride`, `setLocaleOverride`, `setNavigatorOverrides`, `setScriptExecutionDisabled`, `setScrollbarsHidden`, `setTimezoneOverride`, `setTouchEmulationEnabled`, `setUserAgentOverride`
* **Custom Extensions (1)**: `setEmulatedColorSchemeOverride`
* **Missing Standard (26)**: `addScreen`, `clearDevicePostureOverride`, `clearDisplayFeaturesOverride`, `getOverriddenSensorInformation`, `getScreenInfos`, `removeScreen`, `resetPageScaleFactor`, `setAutomationOverride`, `setDataSaverOverride`, `setDevicePostureOverride`, `setDisabledImageTypes`, `setDisplayFeaturesOverride`, `setEmulatedOSTextScale`, `setEmulatedVisionDeficiency`, `setHardwareConcurrencyOverride`, `setPageScaleFactor`, `setPressureSourceOverrideEnabled`, `setPressureStateOverride`, `setPrimaryScreen`, `setSafeAreaInsetsOverride`, `setSensorOverrideEnabled`, `setSensorOverrideReadings`, `setSmallViewportHeightDifferenceOverride`, `setVirtualTimePolicy`, `setVisibleSize`, `updateScreen`

### EventBreakpoints

* **Standard Supported (3)**: `disable`, `removeInstrumentationBreakpoint`, `setInstrumentationBreakpoint`

### Extensions

* **Missing Standard (8)**: `clearStorageItems`, `getExtensions`, `getStorageItems`, `loadUnpacked`, `removeStorageItems`, `setStorageItems`, `triggerAction`, `uninstall`

### FedCm

* **Missing Standard (7)**: `clickDialogButton`, `disable`, `dismissDialog`, `enable`, `openUrl`, `resetCooldown`, `selectAccount`

### Fetch

* **Missing Standard (9)**: `continueRequest`, `continueResponse`, `continueWithAuth`, `disable`, `enable`, `failRequest`, `fulfillRequest`, `getResponseBody`, `takeResponseBodyAsStream`

### FileSystem

* **Standard Supported (1)**: `getDirectory`

### HeadlessExperimental

* **Missing Standard (3)**: `beginFrame`, `disable`, `enable`

### HeapProfiler

* **Missing Standard (12)**: `addInspectedHeapObject`, `collectGarbage`, `disable`, `enable`, `getHeapObjectId`, `getObjectByHeapObjectId`, `getSamplingProfile`, `startSampling`, `startTrackingHeapObjects`, `stopSampling`, `stopTrackingHeapObjects`, `takeHeapSnapshot`

### IndexedDB

* **Missing Standard (9)**: `clearObjectStore`, `deleteDatabase`, `deleteObjectStoreEntries`, `disable`, `enable`, `getMetadata`, `requestData`, `requestDatabase`, `requestDatabaseNames`

### Input

* **Standard Supported (7)**: `dispatchKeyEvent`, `dispatchMouseEvent`, `emulateTouchFromMouseEvent`, `insertText`, `setIgnoreInputEvents`, `synthesizeScrollGesture`, `synthesizeTapGesture`
* **Missing Standard (6)**: `cancelDragging`, `dispatchDragEvent`, `dispatchTouchEvent`, `imeSetComposition`, `setInterceptDrags`, `synthesizePinchGesture`

### Inspector

* **Standard Supported (2)**: `disable`, `enable`

### IO

* **Missing Standard (3)**: `close`, `read`, `resolveBlob`

### LayerTree

* **Missing Standard (9)**: `compositingReasons`, `disable`, `enable`, `loadSnapshot`, `makeSnapshot`, `profileSnapshot`, `releaseSnapshot`, `replaySnapshot`, `snapshotCommandLog`

### Log

* **Standard Supported (5)**: `clear`, `disable`, `enable`, `startViolationsReport`, `stopViolationsReport`

### Media

* **Standard Supported (2)**: `disable`, `enable`

### Memory

* **Standard Supported (5)**: `forciblyPurgeJavaScriptMemory`, `getDOMCounters`, `prepareForLeakDetection`, `setPressureNotificationsSuppressed`, `simulatePressureNotification`
* **Custom Extensions (2)**: `collectGarbage`, `getLiveControls`
* **Missing Standard (6)**: `getAllTimeSamplingProfile`, `getBrowserSamplingProfile`, `getDOMCountersForLeakDetection`, `getSamplingProfile`, `startSampling`, `stopSampling`

### Network

* **Standard Supported (21)**: `canClearBrowserCache`, `canClearBrowserCookies`, `canEmulateNetworkConditions`, `clearAcceptedEncodingsOverride`, `clearBrowserCache`, `clearBrowserCookies`, `deleteCookies`, `disable`, `emulateNetworkConditions`, `enable`, `getAllCookies`, `getCookies`, `getResponseBody`, `setAcceptedEncodings`, `setBlockedURLs`, `setCacheDisabled`, `setCookie`, `setCookies`, `setExtraHTTPHeaders`, `setRequestInterception`, `setUserAgentOverride`
* **Missing Standard (20)**: `configureDurableMessages`, `continueInterceptedRequest`, `deleteDeviceBoundSession`, `emulateNetworkConditionsByRule`, `enableDeviceBoundSessions`, `enableReportingApi`, `fetchSchemefulSite`, `getCertificate`, `getRequestPostData`, `getResponseBodyForInterception`, `getSecurityIsolationStatus`, `loadNetworkResource`, `overrideNetworkState`, `replayXHR`, `searchInResponseBody`, `setAttachDebugStack`, `setBypassServiceWorker`, `setCookieControls`, `streamResourceContent`, `takeResponseBodyForInterceptionAsStream`

### Overlay

* **Standard Supported (13)**: `disable`, `enable`, `hideHighlight`, `highlightNode`, `highlightRect`, `setInspectMode`, `setPausedInDebuggerMessage`, `setShowDebugBorders`, `setShowFlexOverlays`, `setShowFPSCounter`, `setShowGridOverlays`, `setShowPaintRects`, `setShowViewportSizeOnResize`
* **Missing Standard (17)**: `getGridHighlightObjectsForTest`, `getHighlightObjectForTest`, `getSourceOrderHighlightObjectForTest`, `highlightFrame`, `highlightQuad`, `highlightSourceOrder`, `setShowAdHighlights`, `setShowContainerQueryOverlays`, `setShowHinge`, `setShowHitTestBorders`, `setShowInspectedElementAnchor`, `setShowIsolatedElements`, `setShowLayoutShiftRegions`, `setShowScrollBottleneckRects`, `setShowScrollSnapOverlays`, `setShowWebVitals`, `setShowWindowControlsOverlay`

### Page

* **Standard Supported (29)**: `addScriptToEvaluateOnNewDocument`, `bringToFront`, `captureScreenshot`, `clearDeviceMetricsOverride`, `clearDeviceOrientationOverride`, `clearGeolocationOverride`, `disable`, `enable`, `getAppManifest`, `getFrameTree`, `getLayoutMetrics`, `getNavigationHistory`, `getResourceContent`, `getResourceTree`, `navigate`, `reload`, `removeScriptToEvaluateOnNewDocument`, `screencastFrameAck`, `setAdBlockingEnabled`, `setBypassCSP`, `setDeviceMetricsOverride`, `setDeviceOrientationOverride`, `setFontFamilies`, `setFontSizes`, `setGeolocationOverride`, `setLifecycleEventsEnabled`, `setTouchEmulationEnabled`, `startScreencast`, `stopScreencast`
* **Missing Standard (32)**: `addCompilationCache`, `addScriptToEvaluateOnLoad`, `captureSnapshot`, `clearCompilationCache`, `close`, `crash`, `createIsolatedWorld`, `deleteCookie`, `generateTestReport`, `getAdScriptAncestry`, `getAnnotatedPageContent`, `getAppId`, `getInstallabilityErrors`, `getManifestIcons`, `getOriginTrials`, `getPermissionsPolicyState`, `handleJavaScriptDialog`, `navigateToHistoryEntry`, `printToPDF`, `produceCompilationCache`, `removeScriptToEvaluateOnLoad`, `resetNavigationHistory`, `searchInResource`, `setDocumentContent`, `setDownloadBehavior`, `setInterceptFileChooserDialog`, `setPrerenderingAllowed`, `setRPHRegistrationMode`, `setSPCTransactionMode`, `setWebLifecycleState`, `stopLoading`, `waitForDebugger`

### Performance

* **Standard Supported (4)**: `disable`, `enable`, `getMetrics`, `setTimeDomain`

### PerformanceTimeline

* **Standard Supported (1)**: `enable`

### Preload

* **Standard Supported (2)**: `disable`, `enable`

### Profiler

* **Missing Standard (9)**: `disable`, `enable`, `getBestEffortCoverage`, `setSamplingInterval`, `start`, `startPreciseCoverage`, `stop`, `stopPreciseCoverage`, `takePreciseCoverage`

### PWA

* **Missing Standard (7)**: `changeAppUserSettings`, `getOsAppState`, `install`, `launch`, `launchFilesInApp`, `openCurrentPageInApp`, `uninstall`

### Recorder

* **Custom Extensions (2)**: `start`, `stop`

### Runtime

* **Standard Supported (16)**: `addBinding`, `callFunctionOn`, `disable`, `discardConsoleEntries`, `enable`, `evaluate`, `getHeapUsage`, `getIsolateId`, `getProperties`, `releaseObject`, `releaseObjectGroup`, `removeBinding`, `runIfWaitingForDebugger`, `setAsyncCallStackDepth`, `setCustomObjectFormatterEnabled`, `setMaxCallStackSizeToCapture`
* **Missing Standard (7)**: `awaitPromise`, `compileScript`, `getExceptionDetails`, `globalLexicalScopeNames`, `queryObjects`, `runScript`, `terminateExecution`

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

* **Standard Supported (11)**: `activateTarget`, `attachToTarget`, `closeTarget`, `createTarget`, `detachFromTarget`, `exposeDevToolsProtocol`, `getTargetInfo`, `getTargets`, `sendMessageToTarget`, `setAutoAttach`, `setDiscoverTargets`
* **Missing Standard (8)**: `attachToBrowserTarget`, `autoAttachRelated`, `createBrowserContext`, `disposeBrowserContext`, `getBrowserContexts`, `getDevToolsTarget`, `openDevTools`, `setRemoteLocations`

### Tethering

* **Standard Supported (2)**: `bind`, `unbind`

### Tracing

* **Missing Standard (6)**: `end`, `getCategories`, `getTrackEventDescriptor`, `recordClockSyncMarker`, `requestMemoryDump`, `start`

### WebAudio

* **Standard Supported (3)**: `disable`, `enable`, `getRealtimeData`

### WebAuthn

* **Missing Standard (13)**: `addCredential`, `addVirtualAuthenticator`, `clearCredentials`, `disable`, `enable`, `getCredential`, `getCredentials`, `removeCredential`, `removeVirtualAuthenticator`, `setAutomaticPresenceSimulation`, `setCredentialProperties`, `setResponseOverrideBits`, `setUserVerified`

### WebMCP

* **Missing Standard (4)**: `cancelInvocation`, `disable`, `enable`, `invokeTool`

