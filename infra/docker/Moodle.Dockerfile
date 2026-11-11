FROM php:8.3-apache
RUN apt-get update && apt-get install -y --no-install-recommends git curl libicu-dev libzip-dev libpng-dev libjpeg62-turbo-dev libfreetype6-dev libxml2-dev libonig-dev libcurl4-openssl-dev unzip && docker-php-ext-configure gd --with-freetype --with-jpeg && docker-php-ext-install -j$(nproc) mysqli intl zip gd soap opcache exif mbstring && rm -rf /var/lib/apt/lists/*
RUN git clone --depth 1 --branch v5.0.3 https://github.com/moodle/moodle.git /opt/moodle && rm -rf /opt/moodle/.git && cp -a /opt/moodle/. /var/www/html/ && mkdir -p /var/www/moodledata && chown -R www-data:www-data /var/www/html /var/www/moodledata
RUN printf 'max_input_vars=5000\nmemory_limit=256M\nupload_max_filesize=50M\npost_max_size=50M\n' > /usr/local/etc/php/conf.d/moodle.ini
COPY infra/moodle/local/edumonitor /var/www/html/local/edumonitor
COPY infra/moodle/bootstrap.php /opt/bootstrap.php
COPY infra/moodle/sprint2-fixtures.php /opt/sprint2-fixtures.php
COPY infra/moodle/health.php /var/www/html/health.php
COPY infra/moodle/entrypoint.sh /usr/local/bin/moodle-start
RUN sed -i 's/\r$//' /usr/local/bin/moodle-start && chmod +x /usr/local/bin/moodle-start
ENTRYPOINT ["moodle-start"]
CMD ["apache2-foreground"]
