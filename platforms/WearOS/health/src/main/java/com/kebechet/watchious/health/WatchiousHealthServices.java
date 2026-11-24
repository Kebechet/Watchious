package com.kebechet.watchious.health;

import android.content.Context;
import android.os.SystemClock;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.health.services.client.ExerciseClient;
import androidx.health.services.client.ExerciseUpdateCallback;
import androidx.health.services.client.HealthServices;
import androidx.health.services.client.data.Availability;
import androidx.health.services.client.data.DataType;
import androidx.health.services.client.data.ExerciseConfig;
import androidx.health.services.client.data.ExerciseInfo;
import androidx.health.services.client.data.ExerciseLapSummary;
import androidx.health.services.client.data.ExerciseTrackedStatus;
import androidx.health.services.client.data.ExerciseType;
import androidx.health.services.client.data.ExerciseUpdate;
import androidx.health.services.client.data.SampleDataPoint;

import com.google.common.util.concurrent.ListenableFuture;

import java.time.Instant;
import java.util.Collections;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.Executor;
import java.util.concurrent.atomic.AtomicInteger;

// Wear OS Health Services workouts for Watchious.Health, which .NET binds. Like WatchiousDataLayer, only platform
// and Java types appear in its public API; the Health Services callback is a private field.
public final class WatchiousHealthServices {
    public interface Listener {
        void onHeartRate(double beatsPerMinute, long epochMilliseconds);

        // The exercise ended: through endExercise, or on its own when another app's exercise took over.
        void onEnded();
    }

    public interface Callback {
        // error is null on success.
        void onComplete(@Nullable String error);
    }

    public interface RecoveryCallback {
        // isRunning: an exercise of this app still runs, and now reports to the listener.
        void onRecovered(boolean isRunning);
    }

    private final Context context;
    private final ExerciseClient exerciseClient;
    private final Executor executor;
    private final ExerciseUpdateCallback updateCallback;
    // Bumped by every start, recovery and detach: a recovery that finishes after a later detach or start mustn't attach
    // its listener, which may be gone by then.
    private final AtomicInteger attachments = new AtomicInteger();
    @Nullable
    private volatile Listener listener;

    public WatchiousHealthServices(@NonNull Context context) {
        Context applicationContext = context.getApplicationContext();
        this.context = applicationContext;
        exerciseClient = HealthServices.getClient(applicationContext).getExerciseClient();
        executor = applicationContext.getMainExecutor();
        updateCallback = new ExerciseUpdateCallback() {
            @Override
            public void onExerciseUpdateReceived(@NonNull ExerciseUpdate update) {
                deliver(update);
            }

            @Override
            public void onLapSummaryReceived(@NonNull ExerciseLapSummary lapSummary) {
            }

            @Override
            public void onRegistered() {
            }

            @Override
            public void onRegistrationFailed(@NonNull Throwable throwable) {
            }

            @Override
            public void onAvailabilityChanged(@NonNull DataType<?, ?> dataType, @NonNull Availability availability) {
            }
        };
    }

    // exerciseType is Kebechet.Types.Health.Enums.ActivityType's name.
    public void startExercise(@NonNull String exerciseType, @NonNull Listener listener, @NonNull Callback callback) {
        attachments.incrementAndGet();
        this.listener = listener;
        exerciseClient.setUpdateCallback(executor, updateCallback);
        ExerciseConfig config = ExerciseConfig.builder(toExerciseType(exerciseType))
            .setDataTypes(Collections.singleton(DataType.HEART_RATE_BPM))
            .setIsAutoPauseAndResumeEnabled(false)
            .setIsGpsEnabled(false)
            .build();
        complete(exerciseClient.startExerciseAsync(config), error -> {
            // C# forgets a workout that failed to start, so nothing else would remove them.
            if (error != null) {
                this.listener = null;
                exerciseClient.clearUpdateCallbackAsync(updateCallback);
            } else {
                WatchiousWorkoutService.start(context);
            }
            callback.onComplete(error);
        });
    }

    // After the app was closed or restarted: the exercise this app started, if Health Services still runs it, reports
    // to listener again.
    public void recoverExercise(@NonNull Listener listener, @NonNull RecoveryCallback callback) {
        int attachment = attachments.incrementAndGet();
        ListenableFuture<ExerciseInfo> future = exerciseClient.getCurrentExerciseInfoAsync();
        future.addListener(() -> {
            boolean isRunning = false;
            try {
                isRunning = future.get().getExerciseTrackedStatus() == ExerciseTrackedStatus.OWNED_EXERCISE_IN_PROGRESS;
            } catch (ExecutionException exception) {
                isRunning = false;
            } catch (InterruptedException exception) {
                Thread.currentThread().interrupt();
            }
            if (isRunning && attachments.get() == attachment) {
                this.listener = listener;
                exerciseClient.setUpdateCallback(executor, updateCallback);
                WatchiousWorkoutService.start(context);
            } else if (!isRunning) {
                // It ended while the app was away: nothing is left for the foreground service to keep alive.
                exerciseClient.clearUpdateCallbackAsync(updateCallback);
                WatchiousWorkoutService.stop(context);
            }
            callback.onRecovered(isRunning);
        }, executor);
    }

    // The app goes away while the exercise runs on: no more updates to the listener until recoverExercise. The update
    // callback stays registered, since Health Services ends an exercise whose app has none (AUTO_END_MISSING_LISTENER)
    // and the exercise's end, also while the app is away, has to stop the foreground service.
    public void detach() {
        attachments.incrementAndGet();
        listener = null;
    }

    public void pauseExercise(@NonNull Callback callback) {
        complete(exerciseClient.pauseExerciseAsync(), callback);
    }

    public void resumeExercise(@NonNull Callback callback) {
        complete(exerciseClient.resumeExerciseAsync(), callback);
    }

    public void endExercise(@NonNull Callback callback) {
        complete(exerciseClient.endExerciseAsync(), error -> {
            listener = null;
            exerciseClient.clearUpdateCallbackAsync(updateCallback);
            WatchiousWorkoutService.stop(context);
            callback.onComplete(error);
        });
    }

    private void deliver(@NonNull ExerciseUpdate update) {
        Listener current = listener;
        if (current != null) {
            // Data points carry their time since boot.
            Instant bootInstant = Instant.ofEpochMilli(System.currentTimeMillis() - SystemClock.elapsedRealtime());
            for (SampleDataPoint<Double> point : update.getLatestMetrics().getData(DataType.HEART_RATE_BPM)) {
                current.onHeartRate(point.getValue(), point.getTimeInstant(bootInstant).toEpochMilli());
            }
        }
        if (update.getExerciseStateInfo().getState().isEnded()) {
            listener = null;
            exerciseClient.clearUpdateCallbackAsync(updateCallback);
            WatchiousWorkoutService.stop(context);
            if (current != null) {
                current.onEnded();
            }
        }
    }

    private void complete(@NonNull ListenableFuture<?> future, @NonNull Callback callback) {
        future.addListener(() -> {
            String error = null;
            try {
                future.get();
            } catch (ExecutionException exception) {
                Throwable cause = exception.getCause() == null ? exception : exception.getCause();
                error = cause.getMessage() == null ? cause.toString() : cause.getMessage();
            } catch (InterruptedException exception) {
                Thread.currentThread().interrupt();
                error = "Interrupted.";
            }
            callback.onComplete(error);
        }, executor);
    }

    @NonNull
    private static ExerciseType toExerciseType(@NonNull String activityType) {
        switch (activityType) {
            case "Running":
                return ExerciseType.RUNNING;
            case "Walking":
                return ExerciseType.WALKING;
            case "Cycling":
                return ExerciseType.BIKING;
            case "Hiking":
                return ExerciseType.HIKING;
            case "Swimming":
                return ExerciseType.SWIMMING_POOL;
            case "Yoga":
                return ExerciseType.YOGA;
            case "StrengthTraining":
                return ExerciseType.WEIGHTLIFTING;
            case "Elliptical":
                return ExerciseType.ELLIPTICAL;
            case "Rowing":
                return ExerciseType.ROWING_MACHINE;
            case "HighIntensityIntervalTraining":
                return ExerciseType.HIGH_INTENSITY_INTERVAL_TRAINING;
            case "Pilates":
                return ExerciseType.PILATES;
            case "StairClimbing":
                return ExerciseType.STAIR_CLIMBING;
            default:
                return ExerciseType.WORKOUT;
        }
    }
}
